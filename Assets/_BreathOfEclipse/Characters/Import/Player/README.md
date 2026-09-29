# Player model import (VRoid / custom)

1. Put your character here:
   - `Player.vrm` exported from VRoid Studio (install **UniVRM**, MIT, from github.com/vrm-c/UniVRM so Unity turns it into a prefab), or
   - a Humanoid FBX / character prefab.
2. Run **Breath of Eclipse → Characters → Import Player Model**.
   It sets the rig to Humanoid, validates the skeleton (avatar, bones, hands, head, scale), builds a
   `CharacterVisualProfile` (real Quaternius clips + the game's combat poses through IK, anime toon materials,
   katana / sheath / mouth sockets from the hand and head bones) and installs it as the player's VisualModel.
3. **Characters → Use Built-in Anime Swordsman** goes back to the built-in character.

Gameplay (health, damage, breath, stamina, hit detection, skills, AI, saves) never depends on the model;
if it fails validation the game keeps the built-in character, so the player is never invisible.
