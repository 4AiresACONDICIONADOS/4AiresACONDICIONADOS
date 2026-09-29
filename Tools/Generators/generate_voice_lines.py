#!/usr/bin/env python3
"""
Generates the swordsman's Spanish (Latin American) technique calls for BreathingVoiceSystem.

Lines and voice direction come from Scripts/Runtime/Data/DefaultContent.cs, per breathing style:
    style call ("Respiración del Agua"), form calls ("Primera Postura" … "Undécima Postura", "Forma Final")
    and technique names, delivered with the style's VoiceDirection (Water calm, Thunder explosive, Ember deep,
    Gale wild, Moonlight cold; intensity builds from style call → form call → name).
Output (one folder per style, shared fallback clips stay in the language folder):
    Assets/_BreathOfEclipse/Core/Resources/BreathOfEclipse/Voice/es-419/<styleId>/<key>.wav
The key is the text in lower case without accents or punctuation (same rule as VoiceLibrary.Key in C#).
Replace any clip with a real recording of the same name; the game picks it up without code changes.

Providers
    chatterbox (default)  Resemble AI Chatterbox Multilingual, MIT license, local CPU/GPU, built-in generic voice
                          (no cloning, no real actor). Expressiveness via 'exaggeration' / 'cfg_weight'.
                            python3 -m venv tts2
                            tts2/bin/pip install torch==2.6.0 torchaudio==2.6.0 --index-url https://download.pytorch.org/whl/cpu
                            tts2/bin/pip install chatterbox-tts faster-whisper
                            tts2/bin/python Tools/Generators/generate_voice_lines.py
    piper                 Piper 1.2.0 (MIT) + es_MX-ald-medium (dataset Unlicense). The v0.2 placeholder voice.
                            PIPER=tts/bin/piper VOICE=es_MX-ald-medium.onnx python3 ... --provider piper
    elevenlabs            Cloud, paid plan with commercial rights. Needs ELEVENLABS_API_KEY and ELEVENLABS_VOICE_ID
                          (a generic library voice or one you own; never a clone of a real actor). Prepared, untested.

Quality control: every take is transcribed with faster-whisper (MIT, model 'small'); takes whose words do not
match the text, repeat words or run too long are discarded (up to --takes attempts, best one kept). A report is
written to Tools/Generators/voice_report.txt.

Usage: [--provider chatterbox|piper|elevenlabs] [--style tidal] [--force] [--takes 4]
"""
import argparse
import array
import difflib
import json
import os
import re
import subprocess
import sys
import tempfile
import unicodedata
import urllib.request
import wave

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
CONTENT = os.path.join(ROOT, 'Assets/_BreathOfEclipse/Scripts/Runtime/Data/DefaultContent.cs')
OUT = os.path.join(ROOT, 'Assets/_BreathOfEclipse/Core/Resources/BreathOfEclipse/Voice/es-419')
REPORT = os.path.join(ROOT, 'Tools/Generators/voice_report.txt')
ORDINALS = ["Primera", "Segunda", "Tercera", "Cuarta", "Quinta", "Sexta", "Séptima", "Octava", "Novena", "Décima", "Undécima"]

# Emotion → (exaggeration at intensity 0, exaggeration span, cfg_weight at 0, cfg_weight at 1, temperature).
# Lower cfg_weight = slower, more deliberate delivery; higher exaggeration = more intense. Pitch is never shifted.
EMOTIONS = {
    'Neutral':   (0.40, 0.45, 0.50, 0.40, 0.80),
    'Calm':      (0.25, 0.60, 0.50, 0.30, 0.70),   # Water: controlled, elegant, strong final name
    'Explosive': (0.45, 0.60, 0.60, 0.50, 0.80),   # Thunder: fast, explosive, short final line
    'Deep':      (0.35, 0.60, 0.35, 0.25, 0.70),   # Ember: deep, powerful, intense
    'Wild':      (0.50, 0.60, 0.55, 0.45, 0.85),   # Gale: wild, aggressive, quick
    'Cold':      (0.20, 0.45, 0.45, 0.35, 0.65),   # Moonlight: cold, serene, threatening
}


def key(text):
    out, underscore = [], False
    for c in unicodedata.normalize('NFD', text):
        if unicodedata.category(c) == 'Mn':
            continue
        if c.isalnum():
            out.append(c.lower())
            underscore = False
        elif out and not underscore:
            out.append('_')
            underscore = True
    if underscore:
        out.pop()
    return ''.join(out)


def plain(text):
    return ' '.join(key(text).split('_'))


# ---------------------------------------------------------------------------------------------- content

def collect_styles():
    src = open(CONTENT, encoding='utf-8').read()
    blocks = re.split(r'private static BreathingStyleData \w+\(\)', src)[1:]
    styles = []
    for b in blocks:
        sid = re.search(r'Style\("(\w+)"', b)
        call = re.search(r'style\.styleCall = "([^"]+)"', b)
        if not sid or not call:
            continue
        v = re.search(r'style\.voice = new VoiceDirection\(VoiceEmotion\.(\w+), ([\d.]+)f, ([\d.]+)f, ([\d.]+)f, ([\d.]+)f\)', b)
        voice = (v.group(1), float(v.group(2)), float(v.group(3)), float(v.group(4))) if v else ('Neutral', 0.4, 0.5, 0.8)
        forms = [(int(n), name) for n, name in re.findall(r'Form\((\d+), \w+, "([^"]+)"\)', b)]
        ult = re.search(r'Ultimate\(style, \w+, "([^"]+)"\)', b)
        styles.append({'id': sid.group(1), 'call': call.group(1), 'voice': voice, 'forms': forms, 'ultimate': ult.group(1) if ult else None})
    return styles


def lines_for(style):
    emotion, si, fi, ni = style['voice']
    lines = [(style['call'], style['call'] + '.', 'style', si)]
    for number, name in style['forms']:
        call = ORDINALS[number - 1] + ' Postura'
        lines.append((call, call + '.', 'form', fi))
        lines.append((name, '¡' + name + '!', 'name', ni))
    if style['ultimate']:
        lines.append(('Forma Final', 'Forma Final.', 'form', fi))
        lines.append((style['ultimate'], '¡' + style['ultimate'] + '!', 'name', min(1.0, ni + 0.1)))
    seen, unique = set(), []
    for line in lines:
        if key(line[0]) not in seen:
            seen.add(key(line[0]))
            unique.append(line)
    return emotion, unique


# ---------------------------------------------------------------------------------------------- audio

def speech_bounds(data, rate):
    """First continuous speech island: word gaps up to 0.28 s are kept; anything after a longer silence (breath
    tails, repeated words hallucinated by the model) is dropped."""
    frame = max(1, int(rate * 0.02))
    rms = []
    for i in range(0, len(data), frame):
        chunk = data[i:i + frame]
        rms.append((sum(v * v for v in chunk) / max(1, len(chunk))) ** 0.5)
    if not rms:
        return 0, len(data)
    loud = max(rms) * 0.12
    speech = [r > loud for r in rms]
    start = next((i for i, v in enumerate(speech) if v), 0)
    end, gap, max_gap = start, 0, int(0.28 / 0.02)
    for i in range(start, len(speech)):
        if speech[i]:
            end, gap = i, 0
        else:
            gap += 1
            if gap > max_gap:
                break
    # Let the last syllable decay naturally (down to 4% of the loudest frame, at most 0.15 s).
    tail = end
    while tail + 1 < len(rms) and tail - end < int(0.15 / 0.02) and rms[tail + 1] > max(rms) * 0.04:
        tail += 1
    return max(0, start * frame - int(rate * 0.03)), min(len(data), (tail + 1) * frame + int(rate * 0.04))


def trim_and_normalize(path, peak_db=-1.0):
    with wave.open(path, 'rb') as w:
        rate, width, n = w.getframerate(), w.getsampwidth(), w.getnframes()
        data = array.array('h', w.readframes(n))
    if not data:
        return 0.0
    first, last = speech_bounds(data, rate)
    data = data[first:last]
    peak = max(1, max(abs(v) for v in data))
    gain = (32767 * 10 ** (peak_db / 20.0)) / peak
    fade_in, fade_out = int(rate * 0.006), int(rate * 0.04)
    out = array.array('h')
    for i, v in enumerate(data):
        f = min(1.0, i / fade_in if fade_in else 1.0, (len(data) - 1 - i) / fade_out if fade_out else 1.0)
        out.append(int(max(-32767, min(32767, v * gain * f))))
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(out.tobytes())
    return len(out) / rate


# ---------------------------------------------------------------------------------------------- providers

class Chatterbox:
    name = 'chatterbox-multilingual'

    def __init__(self):
        import torch
        from chatterbox.mtl_tts import ChatterboxMultilingualTTS
        torch.set_num_threads(max(1, os.cpu_count() or 1))
        device = 'cuda' if torch.cuda.is_available() else 'cpu'
        self.model = ChatterboxMultilingualTTS.from_pretrained(device=device)

    def params(self, emotion, intensity):
        ex0, span, cfg0, cfg1, temp = EMOTIONS.get(emotion, EMOTIONS['Neutral'])
        return {'exaggeration': round(min(1.05, ex0 + span * intensity), 3),
                'cfg_weight': round(cfg0 + (cfg1 - cfg0) * intensity, 3), 'temperature': temp}

    def synth(self, spoken, path, emotion, intensity):
        import torch
        import torchaudio
        p = self.params(emotion, intensity)
        wav = self.model.generate(spoken, language_id='es', **p)
        torchaudio.save(path, (wav.clamp(-1, 1) * 32767).to(torch.int16), self.model.sr, encoding='PCM_S', bits_per_sample=16)
        return p


class Piper:
    name = 'piper-es_MX-ald-medium'

    def __init__(self):
        self.piper = os.environ.get('PIPER', 'piper')
        self.voice = os.environ.get('VOICE', 'es_MX-ald-medium.onnx')

    def synth(self, spoken, path, emotion, intensity):
        speed = 0.66 if intensity < 0.8 else 0.7
        subprocess.run([self.piper, '--model', self.voice, '--length_scale', str(speed), '--output_file', path],
                       input=spoken.encode('utf-8'), check=True, capture_output=True)
        return {'length_scale': speed}


class ElevenLabs:
    """Prepared for a paid ElevenLabs plan (commercial rights). Untested without a key."""
    name = 'elevenlabs-multilingual-v2'

    def __init__(self):
        self.key = os.environ.get('ELEVENLABS_API_KEY')
        self.voice = os.environ.get('ELEVENLABS_VOICE_ID')
        if not self.key or not self.voice:
            sys.exit('elevenlabs: set ELEVENLABS_API_KEY and ELEVENLABS_VOICE_ID (environment, never in the repo)')

    def synth(self, spoken, path, emotion, intensity):
        settings = {'stability': round(0.7 - 0.45 * intensity, 2), 'similarity_boost': 0.8, 'style': round(0.2 + 0.7 * intensity, 2),
                    'use_speaker_boost': True}
        body = json.dumps({'text': spoken, 'model_id': 'eleven_multilingual_v2', 'voice_settings': settings}).encode('utf-8')
        req = urllib.request.Request(f'https://api.elevenlabs.io/v1/text-to-speech/{self.voice}?output_format=pcm_24000', data=body,
                                     headers={'xi-api-key': self.key, 'Content-Type': 'application/json'})
        pcm = urllib.request.urlopen(req, timeout=60).read()
        with wave.open(path, 'wb') as w:
            w.setnchannels(1)
            w.setsampwidth(2)
            w.setframerate(24000)
            w.writeframes(pcm)
        return settings


# ---------------------------------------------------------------------------------------------- QA

class Checker:
    def __init__(self):
        try:
            from faster_whisper import WhisperModel
            self.model = WhisperModel('small', device='cpu', compute_type='int8')
        except ImportError:
            self.model = None
            print('faster-whisper not installed: takes are not checked')

    def score(self, path, text):
        if self.model is None:
            return 1.0, ''
        segments, _ = self.model.transcribe(path, language='es', beam_size=5, initial_prompt=text)
        heard = ' '.join(s.text for s in segments).strip()
        a, b = plain(text), plain(heard)
        ratio = difflib.SequenceMatcher(None, a, b).ratio()
        # Repeated words ("Serpiente Ascendente. Ascendente.") are a classic long-tail artefact.
        if len(b.split()) > len(a.split()):
            ratio -= 0.25
        return ratio, heard


def max_seconds(text, part):
    return 0.6 + len(text) * (0.12 if part != 'name' else 0.1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--provider', default='chatterbox', choices=['chatterbox', 'piper', 'elevenlabs'])
    ap.add_argument('--style', default=None)
    ap.add_argument('--force', action='store_true')
    ap.add_argument('--takes', type=int, default=4)
    args = ap.parse_args()

    provider = {'chatterbox': Chatterbox, 'piper': Piper, 'elevenlabs': ElevenLabs}[args.provider]()
    checker = Checker()
    report = []
    total = 0
    for style in collect_styles():
        if args.style and style['id'] != args.style:
            continue
        emotion, lines = lines_for(style)
        folder = os.path.join(OUT, style['id'])
        os.makedirs(folder, exist_ok=True)
        for text, spoken, part, intensity in lines:
            path = os.path.join(folder, key(text) + '.wav')
            if os.path.exists(path) and not args.force:
                continue
            best = None
            for take in range(max(1, args.takes)):
                with tempfile.NamedTemporaryFile(suffix='.wav', delete=False) as tmp:
                    tmp_path = tmp.name
                params = provider.synth(spoken, tmp_path, emotion, intensity)
                seconds = trim_and_normalize(tmp_path)
                ratio, heard = checker.score(tmp_path, text)
                if seconds > max_seconds(text, part):
                    ratio -= 0.3
                if best is None or ratio > best[0]:
                    if best is not None:
                        os.remove(best[1])
                    best = (ratio, tmp_path, seconds, heard, params, take + 1)
                else:
                    os.remove(tmp_path)
                if ratio >= 0.9:
                    break
            ratio, tmp_path, seconds, heard, params, takes = best
            os.replace(tmp_path, path)
            os.chmod(path, 0o644)
            flag = 'OK ' if ratio >= 0.9 else 'LOW'
            line = f'{flag} {style["id"]:9s} {key(text):28s} {seconds:4.2f}s score {ratio:4.2f} takes {takes} {emotion}/{part} {params} heard "{heard}"'
            print(line, flush=True)
            report.append(line)
            total += 1
    if report:
        with open(REPORT, 'a', encoding='utf-8') as f:
            f.write(f'# provider {provider.name}\n' + '\n'.join(report) + '\n')
    print(f'{total} clips written under {OUT}')


if __name__ == '__main__':
    sys.exit(main())
