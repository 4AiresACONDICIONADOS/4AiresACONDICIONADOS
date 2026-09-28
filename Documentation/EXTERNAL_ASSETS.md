# External assets

Only resources actually included in the project. Everything else (models, textures, VFX, music, SFX) is generated
by code.

| Asset | Source | URL | License | Author |
|---|---|---|---|---|
| Technique voice clips (`Core/Resources/BreathOfEclipse/Voice/es-419/*.wav`), generated with the tool and voice below | Generated locally with `Tools/Generators/generate_voice_lines.py` | — | Output of the voice model below | — |
| Piper TTS 1.2.0 (tool only, not shipped) | PyPI `piper-tts` | https://github.com/rhasspy/piper | MIT | Michael Hansen / rhasspy |
| Voice model `es_MX-ald-medium` (tool only, not shipped) | Hugging Face `rhasspy/piper-voices` | https://huggingface.co/rhasspy/piper-voices/tree/main/es/es_MX/ald/medium | Dataset: Unlicense (public domain); fine-tuned from `es_ES-davefx` (dataset CC0) | rmcpantoja (dataset), rhasspy |

Placeholder voices: replace any clip with a recording of the same file name (see `VoiceLibrary` in
`Scripts/Runtime/Audio/VoiceChannel.cs`).
