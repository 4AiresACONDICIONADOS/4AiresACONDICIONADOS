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

## Recommended, not included (need the user's account or a manual download)

No external model, texture or animation was added in v0.2.1. Checked licenses (all allow use in a commercial game):

| # | Asset | Use in this project | License |
|---|---|---|---|
| 1 | VRoid Studio (pixiv) + UniVRM | Original anime protagonist → `CharacterVisualProfile.modelPrefab` | VRoid: models you create are yours; UniVRM: MIT |
| 2 | Quaternius Universal Animation Library (https://quaternius.com/packs/universalanimationlibrary.html) | Humanoid locomotion / combat clips for Mecanim | CC0 |
| 3 | Quaternius Stylized Nature MegaKit (https://quaternius.com/packs/stylizednaturemegakit.html) | Trees, rocks, plants for Moonlit Forest | CC0 |
| 4 | Mixamo (Adobe account) | Sword attack / draw / sheathe animations | Royalty-free in projects; no raw redistribution |
| 5 | Quaternius monster packs (https://quaternius.com/) | Base meshes for Nightspawn / Hollow Oni | CC0 |

Never import ripped models or official characters from other franchises.
