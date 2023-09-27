Made by Dreadrith#3238
Discord Server: https://discord.gg/ZsPfrGn

## Feature Release

Window found under DreadTools > Limb Control  

Limb Control allows the control of limbs through Puppet Control with a few clicks.  
Desktop users, you've got power now! Half body users, Kick those people that say you dun got legs!

Setup:  
- Set your Avatar Descriptor  
- Select the limbs to control  
- Press Add Control  
- Done!  

By default, each selected limb is a separate control and is controlled separately.  
Use "Same Control" to make the selected limbs be controlled using the same control.  

Use "Custom BlendTree" to change the way the limbs move by setting your own BlendTree.  

Add Tracking: Adds another Submenu to the Expression Menu which allows Enabling/Disabling tracking on limbs.  
Utilizes the integer values from 244 to 255, integer may be reused for other purposes.  

## Information about the feature
- Toggling a Limb control "On" sets the limb to Animation and enables its ability to be controlled using the "Control" puppet menu.
- Toggling a Limb control "Off" sets the limb to Tracking and disables the ability to control it using the "Control" puppet menu.
- Limb Control works during normal movement, emote animation and while sitting.

## Information about the script
- Duplicates Base, Action and Sitting controllers if they exist. Creates new ones if they don't.
- Creates a new Expression Menu and/or Expression Parameters if they don't exist.
- Adds Submenu controls to the main Expression Menu. Fails if full.
- Creates new assets at the chosen asset path in the window.
