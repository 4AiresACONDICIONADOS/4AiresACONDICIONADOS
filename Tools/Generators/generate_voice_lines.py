#!/usr/bin/env python3
"""
Generates the placeholder Spanish technique voice clips (BreathingVoiceSystem) with Piper TTS.

Lines come from Scripts/Runtime/Data/DefaultContent.cs: every style call, every form call ("Primera Postura" …
"Undécima Postura", "Forma Final") and every technique name. Output:
    Assets/_BreathOfEclipse/Core/Resources/BreathOfEclipse/Voice/es-419/<key>.wav
The key is the text in lower case without accents or punctuation (same rule as VoiceLibrary.Key in C#).
Replace any clip with a real recording of the same name; the game picks it up without code changes.

Requirements (not part of the game):
    python3 -m venv tts && tts/bin/pip install piper-tts==1.2.0       # Piper 1.2.0, MIT
    voice es_MX-ald-medium (.onnx + .onnx.json) from huggingface.co/rhasspy/piper-voices
        dataset: Ald Mexican Spanish speech dataset, Unlicense (public domain)
Usage:
    PIPER=tts/bin/piper VOICE=es_MX-ald-medium.onnx python3 Tools/Generators/generate_voice_lines.py
"""
import array
import os
import re
import subprocess
import sys
import tempfile
import unicodedata
import wave

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
CONTENT = os.path.join(ROOT, 'Assets/_BreathOfEclipse/Scripts/Runtime/Data/DefaultContent.cs')
OUT = os.path.join(ROOT, 'Assets/_BreathOfEclipse/Core/Resources/BreathOfEclipse/Voice/es-419')
ORDINALS = ["Primera", "Segunda", "Tercera", "Cuarta", "Quinta", "Sexta", "Séptima", "Octava", "Novena", "Décima", "Undécima"]


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


def collect_lines():
    src = open(CONTENT, encoding='utf-8').read()
    styles = re.findall(r'styleCall = "([^"]+)"', src)
    names = re.findall(r'Form\(\d+, \w+, "([^"]+)"\)', src) + re.findall(r'Ultimate\(style, \w+, "([^"]+)"\)', src)
    forms = [o + " Postura" for o in ORDINALS] + ["Forma Final"]
    # (text, spoken text, speed): names are shouted, calls are steady.
    lines = [(t, t + ".", 0.66) for t in styles] + [(t, t + ".", 0.66) for t in forms] + [(t, "¡" + t + "!", 0.7) for t in names]
    seen, unique = set(), []
    for text, spoken, speed in lines:
        if key(text) not in seen:
            seen.add(key(text))
            unique.append((text, spoken, speed))
    return unique


def trim_and_normalize(path):
    with wave.open(path, 'rb') as w:
        rate, width, n = w.getframerate(), w.getsampwidth(), w.getnframes()
        data = array.array('h', w.readframes(n))
    peak = max(1, max(abs(v) for v in data))
    threshold = peak * 0.02
    pad = int(rate * 0.03)
    first = next((i for i, v in enumerate(data) if abs(v) > threshold), 0)
    last = len(data) - next((i for i, v in enumerate(reversed(data)) if abs(v) > threshold), 0)
    data = data[max(0, first - pad):min(len(data), last + pad)]
    gain = (32767 * 0.89) / peak  # -1 dBFS
    fade = int(rate * 0.008)
    out = array.array('h')
    for i, v in enumerate(data):
        f = min(1.0, i / fade, (len(data) - 1 - i) / fade) if fade > 0 else 1.0
        out.append(int(max(-32767, min(32767, v * gain * f))))
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(width)
        w.setframerate(rate)
        w.writeframes(out.tobytes())
    return len(out) / rate


def main():
    piper = os.environ.get('PIPER', 'piper')
    voice = os.environ.get('VOICE', 'es_MX-ald-medium.onnx')
    os.makedirs(OUT, exist_ok=True)
    lines = collect_lines()
    for text, spoken, speed in lines:
        path = os.path.join(OUT, key(text) + '.wav')
        with tempfile.NamedTemporaryFile(suffix='.wav', delete=False) as tmp:
            tmp_path = tmp.name
        subprocess.run([piper, '--model', voice, '--length_scale', str(speed), '--output_file', tmp_path],
                       input=spoken.encode('utf-8'), check=True, capture_output=True)
        os.replace(tmp_path, path)
        seconds = trim_and_normalize(path)
        print(f'{key(text):32s} {seconds:4.2f}s  {spoken}')
    print(f'{len(lines)} clips in {OUT}')


if __name__ == '__main__':
    sys.exit(main())
