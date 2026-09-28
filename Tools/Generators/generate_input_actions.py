#!/usr/bin/env python3
"""Generates the default Input Actions asset and the matching C# fallback.

The .inputactions asset (Assets/_BreathOfEclipse/Core/Resources/...) is the editable source of truth inside Unity.
The C# fallback (DefaultInputActions.cs) is only used if the asset is missing, so the game never ships without
controls. Re-run after changing the default bindings here:  python3 Tools/Generators/generate_input_actions.py
"""
import json, uuid, os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
NS = uuid.UUID('6f1c1f6e-9a55-4c4b-9a8b-b0e1e0c1b0e5')

def gid(name):
    return str(uuid.uuid5(NS, name))

KM = 'Keyboard&Mouse'
GP = 'Gamepad'

def action(name, typ='Button', control='Button'):
    a = {"name": name, "type": typ, "id": gid('action/' + name), "expectedControlType": control,
         "processors": "", "interactions": "", "initialStateCheck": typ == 'Value'}
    return a

def binding(action_name, path, group, name=''):
    return {"name": name, "id": gid('binding/' + action_name + '/' + path + '/' + name), "path": path,
            "interactions": "", "processors": "", "groups": group, "action": action_name,
            "isComposite": False, "isPartOfComposite": False}

def composite(action_name, name, parts, group):
    out = [{"name": name, "id": gid('composite/' + action_name + '/' + name), "path": "2DVector",
            "interactions": "", "processors": "", "groups": "", "action": action_name,
            "isComposite": True, "isPartOfComposite": False}]
    for part, path in parts:
        b = binding(action_name, path, group, part)
        b["isPartOfComposite"] = True
        out.append(b)
    return out

gameplay_actions = [
    action('Move', 'Value', 'Vector2'),
    action('Look', 'PassThrough', 'Vector2'),
    action('LookStick', 'Value', 'Vector2'),
    action('Sprint'), action('Jump'), action('Dodge'),
    action('LightAttack'), action('HeavyAttack'), action('Block'),
    action('LockOn'), action('SwitchTarget', 'PassThrough', 'Axis'),
    action('Skill1'), action('Skill2'), action('Skill3'), action('Skill4'),
    action('Ultimate'), action('CameraMode'), action('Interact'),
    action('NextStyle'), action('PrevStyle'),
]

b = []
b += composite('Move', 'WASD', [('up', '<Keyboard>/w'), ('down', '<Keyboard>/s'), ('left', '<Keyboard>/a'), ('right', '<Keyboard>/d')], KM)
b += composite('Move', 'Arrows', [('up', '<Keyboard>/upArrow'), ('down', '<Keyboard>/downArrow'), ('left', '<Keyboard>/leftArrow'), ('right', '<Keyboard>/rightArrow')], KM)
b.append(binding('Move', '<Gamepad>/leftStick', GP))
b.append(binding('Look', '<Mouse>/delta', KM))
b.append(binding('LookStick', '<Gamepad>/rightStick', GP))
pairs = [
    ('Sprint', '<Keyboard>/leftShift', '<Gamepad>/leftStickPress'),
    ('Jump', '<Keyboard>/space', '<Gamepad>/buttonSouth'),
    ('Dodge', '<Keyboard>/leftAlt', '<Gamepad>/buttonEast'),
    ('LightAttack', '<Mouse>/leftButton', '<Gamepad>/buttonWest'),
    ('HeavyAttack', '<Mouse>/rightButton', '<Gamepad>/buttonNorth'),
    ('Block', '<Keyboard>/q', '<Gamepad>/leftShoulder'),
    ('LockOn', '<Mouse>/middleButton', '<Gamepad>/rightStickPress'),
    ('Skill1', '<Keyboard>/1', '<Gamepad>/dpad/up'),
    ('Skill2', '<Keyboard>/2', '<Gamepad>/dpad/right'),
    ('Skill3', '<Keyboard>/3', '<Gamepad>/dpad/down'),
    ('Skill4', '<Keyboard>/4', '<Gamepad>/dpad/left'),
    ('Ultimate', '<Keyboard>/r', '<Gamepad>/rightTrigger'),
    ('CameraMode', '<Keyboard>/c', '<Gamepad>/select'),
    ('Interact', '<Keyboard>/e', '<Gamepad>/leftTrigger'),
    ('NextStyle', '<Keyboard>/x', '<Gamepad>/rightShoulder'),
]
for name, kb, gp in pairs:
    b.append(binding(name, kb, KM))
    b.append(binding(name, gp, GP))
b.append(binding('Dodge', '<Keyboard>/rightAlt', KM))
b.append(binding('LockOn', '<Keyboard>/tab', KM))
b.append(binding('SwitchTarget', '<Mouse>/scroll/y', KM))
b.append(binding('PrevStyle', '<Keyboard>/z', KM))

system_actions = [action('Pause'), action('DebugMenu'), action('ToggleFps')]
sb = [
    binding('Pause', '<Keyboard>/escape', KM), binding('Pause', '<Gamepad>/start', GP),
    binding('DebugMenu', '<Keyboard>/f1', KM),
    binding('ToggleFps', '<Keyboard>/f2', KM),
]

asset = {
    "name": "BreathOfEclipseControls",
    "maps": [
        {"name": "Gameplay", "id": gid('map/Gameplay'), "actions": gameplay_actions, "bindings": b},
        {"name": "System", "id": gid('map/System'), "actions": system_actions, "bindings": sb},
    ],
    "controlSchemes": [
        {"name": KM, "bindingGroup": KM, "devices": [
            {"devicePath": "<Keyboard>", "isOptional": False, "isOR": False},
            {"devicePath": "<Mouse>", "isOptional": False, "isOR": False}]},
        {"name": GP, "bindingGroup": GP, "devices": [
            {"devicePath": "<Gamepad>", "isOptional": False, "isOR": False}]},
    ],
}

text = json.dumps(asset, indent=4)
out_asset = os.path.join(ROOT, 'Assets/_BreathOfEclipse/Core/Resources/BreathOfEclipse/Input/BreathOfEclipseControls.inputactions')
os.makedirs(os.path.dirname(out_asset), exist_ok=True)
with open(out_asset, 'w') as f:
    f.write(text)

cs = '''// <auto-generated> by Tools/Generators/generate_input_actions.py. Do not edit by hand. </auto-generated>
namespace BreathOfEclipse.Core
{
    /// <summary>Fallback copy of the default controls, used only if the Input Actions asset cannot be loaded.</summary>
    public static class DefaultInputActions
    {
        public const string Json = @"%s";
    }
}
''' % text.replace('"', '""')
out_cs = os.path.join(ROOT, 'Assets/_BreathOfEclipse/Scripts/Runtime/Input/DefaultInputActions.cs')
with open(out_cs, 'w') as f:
    f.write(cs)
print('wrote', out_asset, out_cs)
