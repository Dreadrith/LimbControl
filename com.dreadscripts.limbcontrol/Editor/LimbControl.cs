using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRC.SDK3.Avatars.Components;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.ScriptableObjects;
using System;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using Object = UnityEngine.Object;
using static DreadScripts.LimbControl.LimbControl.CustomGUI;
using UnityEditor.SceneManagement;
using UnityEngine.Networking;

namespace DreadScripts.LimbControl
{
    public class LimbControl : EditorWindow
    {
	    private const string LAYER_TAG = "Limb Control";


		public static VRCAvatarDescriptor avatar;
        public static bool useSameParameter;
        public static bool useCustomTree;
        public static bool addTracking;
        public static bool addRightArm, addLeftArm, addRightLeg, addLeftLeg;

        public static BlendTree customTree;

		private static string assetPath;
        private static AnimationClip emptyClip;
		private static Vector2 scroll;


		[MenuItem("DreadTools/Limb Control", false, 566)]
        public static void ShowWindow()
        {
            GetWindow<LimbControl>("Limb Control").titleContent.image = EditorGUIUtility.IconContent("AvatarMask Icon").image;
        }

        public void OnGUI()
        {
	        bool isValid = true;
            scroll = EditorGUILayout.BeginScrollView(scroll);

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                avatar = (VRCAvatarDescriptor)EditorGUILayout.ObjectField(Content.avatarContent, avatar, typeof(VRCAvatarDescriptor), true);
                isValid &= avatar;

            }

            EditorGUILayout.Space();
            using (new GUILayout.VerticalScope(GUI.skin.box))
            {
	            DrawTitle("Control Limbs", "Choose which Limbs to control in your menu");
	            
                using (new GUILayout.HorizontalScope())
                {
                    DrawColoredButton(ref addLeftArm, "Left Arm");
                    DrawColoredButton(ref addRightArm, "Right Arm");
                }

                using (new GUILayout.HorizontalScope())
                {
                    DrawColoredButton(ref addLeftLeg, "Left Leg");
					DrawColoredButton(ref addRightLeg, "Right Leg");
                }
				DrawColoredButton(ref addTracking, Content.trackingContent);
                EditorGUILayout.Space();
            }

            using (new GUILayout.VerticalScope(GUI.skin.box))
            {
	            DrawTitle("Extra", "To Add to or Modify Limb Control");
	            bool canCombineControl = Convert.ToInt32(addRightArm) + Convert.ToInt32(addLeftArm) + Convert.ToInt32(addLeftLeg) + Convert.ToInt32(addRightLeg) > 1;
	            if (!canCombineControl) useSameParameter = false;
	            using (new EditorGUI.DisabledScope(!canCombineControl))
		            DrawColoredButton(ref useSameParameter, Content.sameControlContent);


	            if (!useCustomTree) DrawColoredButton(ref useCustomTree, Content.customTreeContent);
	            else
	            {
		            using (new GUILayout.HorizontalScope())
		            {
			            using (new BGColoredScope(useCustomTree))
				            useCustomTree = GUILayout.Toggle(useCustomTree, string.Empty, EditorStyles.toolbarButton, GUILayout.Width(18), GUILayout.Height(18));
			            customTree = (BlendTree)EditorGUILayout.ObjectField(customTree, typeof(BlendTree), false);
		            }
	            }
			}

			EditorGUILayout.Space();
			EditorGUILayout.Space();
	        GUILayout.Label(new GUIContent("Required Memory: 0!","Limb Control only needs VRC's IK sync and no parameter syncing!"), EditorStyles.centeredGreyMiniLabel);
            

	        isValid &= addLeftArm || addLeftLeg || addRightArm || addRightLeg || addTracking;

            using (new GUILayout.HorizontalScope())
            {
                using (new BGColoredScope(isValid))
                using(new EditorGUI.DisabledScope(!isValid))
                    if (GUILayout.Button("Apply Limb Control", Content.comicallyLargeButton, GUILayout.Height(32)))
                        InitAddControl(!(addLeftArm || addLeftLeg || addRightArm || addRightLeg));
                
                using (new BGColoredScope(Color.red))
                using (new EditorGUI.DisabledScope(!avatar))
					if (GUILayout.Button(Content.iconTrash,new GUIStyle(GUI.skin.button) {padding=new RectOffset()},GUILayout.Width(40),GUILayout.Height(32)))
						if (EditorUtility.DisplayDialog("Remove Limb Control", "Remove Limb Control from " + avatar.gameObject.name + "?\nThis action can't be reverted.", "Remove", "Cancel"))
							RemoveLimbControl();
            }

            DrawSeparator();
            assetPath = AssetFolderPath(assetPath, "Generated Assets", "LimbControlSavePath");
            Credit();

            EditorGUILayout.EndScrollView();
        }

        private static string folderPath;
        private static void InitAddControl(bool onlyTracking=false)
        {

            if (avatar.expressionsMenu)
            {
                int cCost = (!onlyTracking ? 1 : 0) + (addTracking ? 1 : 0);
                if (onlyTracking && !avatar.expressionsMenu.controls.Any(c => c.name == "Limb Control" && c.type == VRCExpressionsMenu.Control.ControlType.SubMenu && c.subMenu))
                {
                    cCost -= 1;
                }
                if (addTracking && !avatar.expressionsMenu.controls.Any(c => c.name == "Tracking Control" && c.type == VRCExpressionsMenu.Control.ControlType.SubMenu && c.subMenu))
                {
                    cCost -= 1;
                }

                if ( 8 - (avatar.expressionsMenu.controls.Count + cCost) < 0)
                {
                    Debug.LogError("Expression Menu can't contain more than 8 controls!");
                    return;
                }
            
            }

            ReadyPath(assetPath);
            folderPath = AssetDatabase.GenerateUniqueAssetPath($"{assetPath}/{ValidatePath(avatar.gameObject.name)}");
            ReadyPath(folderPath);
            emptyClip = Resources.Load<AnimationClip>("Animations/LC_EmptyClip");

            AnimatorController myLocomotion = GetPlayableLayer(avatar, VRCAvatarDescriptor.AnimLayerType.Base);
            if (!myLocomotion)
                myLocomotion = Resources.Load<AnimatorController>("Animations/LC_BaseLocomotion");

            myLocomotion = CopyAssetAndReturn<AnimatorController>(myLocomotion, AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + myLocomotion.name + ".controller"));
            SetPlayableLayer(avatar, VRCAvatarDescriptor.AnimLayerType.Base, myLocomotion);

            if (onlyTracking) goto AddTrackingJump;

            AnimatorController myAction = GetPlayableLayer(avatar, VRCAvatarDescriptor.AnimLayerType.Action);
            if (!myAction)
            {
                myAction = new AnimatorController() { name = "LC_BaseAction" };
                AssetDatabase.CreateAsset(myAction, folderPath + "/" + myAction.name + ".controller");
                myAction.AddLayer("Base Layer");
            }
            else
                myAction = CopyAssetAndReturn<AnimatorController>(myAction, AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + myAction.name + ".controller"));
            SetPlayableLayer(avatar, VRCAvatarDescriptor.AnimLayerType.Action, myAction);

            AnimatorController mySitting = GetPlayableLayer(avatar, VRCAvatarDescriptor.AnimLayerType.Sitting);
            if (!mySitting)
                mySitting = Resources.Load<AnimatorController>("Animations/LC_BaseSitting");

            mySitting = CopyAssetAndReturn<AnimatorController>(mySitting, AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + mySitting.name + ".controller"));
            SetPlayableLayer(avatar, VRCAvatarDescriptor.AnimLayerType.Sitting, mySitting);

            AvatarMask GetNewMask(string n)
            {
                AvatarMask newMask = new AvatarMask() { name = n };
                for (int i = 0; i < 13; i++)
                {
                    newMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
                }
                AssetDatabase.CreateAsset(newMask, AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + newMask.name + ".mask"));
                return newMask;
            }

            AvatarMask myMask = null;
            if (useSameParameter)
                myMask = GetNewMask("LC_MixedMask");

            GetBaseTree();

            void DoControl(bool isRight, bool isArm)
            {
                string direction = isRight? "Right" : "Left";
                string limbName = isArm ? "Arm" : "Leg";
                string fullName = direction + limbName;

                if (!useSameParameter)
                    myMask = GetNewMask($"LC_{fullName}Mask");

                myMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)Enum.Parse(typeof(AvatarMaskBodyPart), fullName, true), true);
                if (!useSameParameter)
                    AddControl(myMask, customTree, $"LC/{fullName}");
            }

            if (addRightArm) DoControl(true, true);
            if (addRightLeg) DoControl(true, false);
            if (addLeftLeg) DoControl(false, false);
            if (addLeftArm) DoControl(false, true);


            if (useSameParameter)
            {
                string newBaseParameter = "Mixed";
                
                if (avatar.expressionParameters)
                    newBaseParameter = GenerateUniqueString(newBaseParameter, s => avatar.expressionParameters.parameters.All(p => p.name != s));
                AddControl(myMask, customTree, $"LC/{newBaseParameter}");
            }

            Debug.Log("<color=green>[Limb Control]</color> Added Limb Control successfully!");

            AddTrackingJump:
            if (addTracking) AddTracking(myLocomotion);
        }
		public static void AddControl(AvatarMask mask, BlendTree tree, string baseparameter)
		{
			var tempParameter = $"{baseparameter}/Temp";
			var toggleParameter = $"{baseparameter}/Toggle";
            var treeParameter1 = $"{baseparameter}/X";
            var treeParameter2 = $"{baseparameter}/Y";
            avatar.customExpressions = true;
            avatar.customizeAnimationLayers = true;

            VRCExpressionsMenu myMenu = avatar.expressionsMenu;
            VRCExpressionParameters myParams = avatar.expressionParameters;

            if (!myMenu)
                myMenu = ReplaceMenu(avatar, folderPath);

            if (!myParams)
                myParams = ReplaceParameters(avatar, folderPath);

            

            VRCExpressionsMenu mainMenu = myMenu.controls.Find(c => c.name == "Limb Control" && c.type == VRCExpressionsMenu.Control.ControlType.SubMenu && c.subMenu)?.subMenu;

            if (mainMenu == null)
            {
                mainMenu = CreateInstance<VRCExpressionsMenu>();
                mainMenu.controls = new List<VRCExpressionsMenu.Control>();
                AddControls(myMenu, new List<VRCExpressionsMenu.Control>() { new VRCExpressionsMenu.Control() { name = "Limb Control", type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = mainMenu, icon = Resources.Load<Texture2D>("Icons/LC_HandWaving") } });
                AssetDatabase.CreateAsset(mainMenu, folderPath + "/LC_LimbControlMainMenu.asset");
            }

            VRCExpressionsMenu mySubmenu = Resources.Load<VRCExpressionsMenu>("LC_LimbControlMenu");
            var subMenuPath = $"{folderPath}/{ValidateName(baseparameter)} Control Menu.asset";
            mySubmenu = CopyAssetAndReturn(mySubmenu, subMenuPath);

			mySubmenu.controls[0].value = 1;
            mySubmenu.controls[0].parameter = new VRCExpressionsMenu.Control.Parameter() { name = toggleParameter };

            VRCExpressionsMenu.Control.Parameter[] subParameters = new VRCExpressionsMenu.Control.Parameter[2];
            subParameters[0] = new VRCExpressionsMenu.Control.Parameter { name = treeParameter1 };
            subParameters[1] = new VRCExpressionsMenu.Control.Parameter() { name = treeParameter2 };

            mySubmenu.controls[1].parameter = new VRCExpressionsMenu.Control.Parameter(){name = tempParameter };
            mySubmenu.controls[1].subParameters = subParameters;

            EditorUtility.SetDirty(mySubmenu);

            var indStart = toggleParameter.IndexOf('/')+1;
            var indEnd = toggleParameter.LastIndexOf('/');
            if (indStart == indEnd)
	            indStart = 0;
            if (indEnd == 0)
                indEnd = toggleParameter.Length;

            var midName = toggleParameter.Substring(indStart, indEnd - indStart);
            var finalName = midName.Aggregate(string.Empty, (result, next) =>
            {
	            if (char.IsUpper(next) && result.Length > 0)
		            result += ' ';
	            return result + next;
            });

			VRCExpressionsMenu.Control newControl = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                name = finalName,
                subMenu = mySubmenu

            };
            AddControls(mainMenu, new List<VRCExpressionsMenu.Control>() { newControl });

            AddParameters(myParams, new List<VRCExpressionParameters.Parameter>() {
            new VRCExpressionParameters.Parameter(){ name = toggleParameter,      saved = false,  valueType = VRCExpressionParameters.ValueType.Bool, defaultValue=0, networkSynced = false },
            new VRCExpressionParameters.Parameter(){ name = treeParameter1, saved = true,   valueType = VRCExpressionParameters.ValueType.Float, networkSynced = false  },
            new VRCExpressionParameters.Parameter(){ name = treeParameter2, saved = true,   valueType = VRCExpressionParameters.ValueType.Float, networkSynced = false  }
            });
        
            BlendTree myTree = CopyAssetAndReturn<BlendTree>(tree, AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + tree.name + ".blendtree"));
            myTree.blendParameter = treeParameter1;
            myTree.blendParameterY = treeParameter2;
            EditorUtility.SetDirty(myTree);

            void AddLimbLayer(AnimatorController controller, bool setTracking)
            {
				ReadyParameter(controller, tempParameter, AnimatorControllerParameterType.Bool);
	            ReadyParameter(controller, toggleParameter, AnimatorControllerParameterType.Bool);
                ReadyParameter(controller, treeParameter1, AnimatorControllerParameterType.Float);
                ReadyParameter(controller, treeParameter2, AnimatorControllerParameterType.Float);

                AnimatorControllerLayer newLayer = AddLayer(controller, toggleParameter, 1, mask); 
                AddTag(newLayer, LAYER_TAG);

                AnimatorState firstState = newLayer.stateMachine.AddState("Idle", new Vector3(30, 160));
                AnimatorState secondState = newLayer.stateMachine.AddState("Control", new Vector3(30, 210));

                firstState.motion = emptyClip;
                secondState.motion = myTree;

                if (setTracking)
                {
                    VRCAnimatorTrackingControl trackingControl = firstState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
                    VRCAnimatorTrackingControl trackingControl2 = secondState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();

                    if (mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Head))
                    {
                        trackingControl.trackingHead = VRCAnimatorTrackingControl.TrackingType.Tracking;
                        trackingControl2.trackingHead = VRCAnimatorTrackingControl.TrackingType.Animation;
                    }

                    if (mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Body))
                    {
                        trackingControl.trackingHip = VRCAnimatorTrackingControl.TrackingType.Tracking;
                        trackingControl2.trackingHip = VRCAnimatorTrackingControl.TrackingType.Animation;
                    }

                    if (mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm))
                    {
                        trackingControl.trackingRightHand = VRCAnimatorTrackingControl.TrackingType.Tracking;
                        trackingControl2.trackingRightHand = VRCAnimatorTrackingControl.TrackingType.Animation;
                    }

                    if (mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm))
                    {
                        trackingControl.trackingLeftHand = VRCAnimatorTrackingControl.TrackingType.Tracking;
                        trackingControl2.trackingLeftHand = VRCAnimatorTrackingControl.TrackingType.Animation;
                    }

                    if (mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers))
                    {
                        trackingControl.trackingRightFingers = VRCAnimatorTrackingControl.TrackingType.Tracking;
                        trackingControl2.trackingRightFingers = VRCAnimatorTrackingControl.TrackingType.Animation;
                    }

                    if (mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers))
                    {
                        trackingControl.trackingLeftFingers = VRCAnimatorTrackingControl.TrackingType.Tracking;
                        trackingControl2.trackingLeftFingers = VRCAnimatorTrackingControl.TrackingType.Animation;
                    }

                    if (mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg))
                    {
                        trackingControl.trackingRightFoot = VRCAnimatorTrackingControl.TrackingType.Tracking;
                        trackingControl2.trackingRightFoot = VRCAnimatorTrackingControl.TrackingType.Animation;
                    }

                    if (mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg))
                    {
                        trackingControl.trackingLeftFoot = VRCAnimatorTrackingControl.TrackingType.Tracking;
                        trackingControl2.trackingLeftFoot = VRCAnimatorTrackingControl.TrackingType.Animation;
                    }

                }

                var t = firstState.AddTransition(secondState, false);
                t.duration = 0.15f;
                t.AddCondition(AnimatorConditionMode.If, 0, toggleParameter);

                t = firstState.AddTransition(secondState, false);
                t.duration = 0.15f;
                t.AddCondition(AnimatorConditionMode.If, 0, tempParameter);

                t = secondState.AddTransition(firstState, false);
                t.duration = 0.15f;
                t.AddCondition(AnimatorConditionMode.IfNot, 0, toggleParameter);
                t.AddCondition(AnimatorConditionMode.IfNot, 0, tempParameter);
                
            }


            AnimatorController myLocomotion = GetPlayableLayer(avatar, VRCAvatarDescriptor.AnimLayerType.Base);
            AddLimbLayer(myLocomotion, true);

            AnimatorController myAction = GetPlayableLayer(avatar, VRCAvatarDescriptor.AnimLayerType.Action);
            AddLimbLayer(myAction, false);

            AnimatorController mySitting = GetPlayableLayer(avatar, VRCAvatarDescriptor.AnimLayerType.Sitting);
            AddLimbLayer(mySitting, false);

            EditorUtility.SetDirty(avatar);
            EditorSceneManager.MarkAllScenesDirty();
        }

        private static void AddTracking(AnimatorController c)
        {
            ReadyParameter(c, "LC/Tracking Control", AnimatorControllerParameterType.Int);

            VRCExpressionsMenu myMenu = avatar.expressionsMenu;
            VRCExpressionParameters myParams = avatar.expressionParameters;

            if (!myMenu)
                myMenu = ReplaceMenu(avatar, folderPath);

            if (!myParams)
                myParams = ReplaceParameters(avatar, folderPath);

            VRCExpressionsMenu trackingMenu = Resources.Load<VRCExpressionsMenu>("LC_TrackingMainMenu");
            trackingMenu = ReplaceMenu(trackingMenu, folderPath, true);

            AddControls(myMenu, new List<VRCExpressionsMenu.Control>() { new VRCExpressionsMenu.Control() {name="Tracking Control", type= VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = trackingMenu, icon = Resources.Load<Texture2D>("Icons/LC_RnR") } });
            AddParameters(myParams, new List<VRCExpressionParameters.Parameter>() { new VRCExpressionParameters.Parameter() { name = "LC/Tracking Control", saved = false, valueType = VRCExpressionParameters.ValueType.Int, networkSynced = false } });

            AnimatorControllerLayer newLayer = AddLayer(c, "LC/Tracking Control", 0);
            AddTag(newLayer, LAYER_TAG);

            AnimatorStateMachine m = newLayer.stateMachine;
            AnimatorState HoldState = m.AddState("Hold", new Vector3(260, 120));
            HoldState.motion = emptyClip;

            m.exitPosition = new Vector3(760, 120);
            Vector3 startIndex = new Vector3(510, -140);
            Vector3 GetNextPos()
            {
	            startIndex += new Vector3(0, 40);
	            return startIndex;
            }

            void AddTrackingState(string propertyName,string partName, int toggleValue)
            {
                AnimatorState enableState = m.AddState(partName + " On", GetNextPos());
                AnimatorState disableState = m.AddState(partName + " Off", GetNextPos());
                enableState.motion = disableState.motion = emptyClip;

                InstantTransition(HoldState, enableState).AddCondition(AnimatorConditionMode.Equals, toggleValue, "LC/Tracking Control");
                InstantExitTransition(enableState).AddCondition(AnimatorConditionMode.NotEqual, toggleValue, "LC/Tracking Control");

                InstantTransition(HoldState, disableState).AddCondition(AnimatorConditionMode.Equals, toggleValue+1, "LC/Tracking Control");
                InstantExitTransition(disableState).AddCondition(AnimatorConditionMode.NotEqual, toggleValue+1, "LC/Tracking Control");

                void setSObject(SerializedObject o, int v)
                {
                    o.FindProperty(propertyName).enumValueIndex = v;
                    o.ApplyModifiedPropertiesWithoutUndo();
                }

                setSObject(new SerializedObject(enableState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>()), 1);
                setSObject(new SerializedObject(disableState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>()), 2);
            }

            AddTrackingState("trackingHead", "Head", 254);
            AddTrackingState("trackingRightHand", "Right Hand", 252);
            AddTrackingState("trackingLeftHand", "Left Hand", 250);
            AddTrackingState("trackingHip", "Hip", 248);
            AddTrackingState("trackingRightFoot", "Right Foot", 246);
            AddTrackingState("trackingLeftFoot", "Left Foot", 244);

            Debug.Log("<color=green>[Limb Control]</color> Added Tracking Control successfully!");
        }

        private static void RemoveLimbControl()
        {
            Debug.Log("Removing Limb Control from " + avatar.gameObject.name);
            void RemoveControl(AnimatorController c)
            {
                if (!c)
                    return;
                for (int i=c.layers.Length-1;i>=0;i--)
                {
                    if (HasTag(c.layers[i], LAYER_TAG))
                    {
                        Debug.Log("Removed Layer " + c.layers[i].name+" from "+c.name);
                        c.RemoveLayer(i);
                    }
                }
                for (int i=c.parameters.Length-1;i>=0;i--)
                {
                    if (c.parameters[i].name.StartsWith("LC/"))
                    {
                        Debug.Log("Removed Parameter " + c.parameters[i].name + " from " + c.name);
                        c.RemoveParameter(i);
                    }
                }
            }
            RemoveControl(GetPlayableLayer(avatar, VRCAvatarDescriptor.AnimLayerType.Base));
            RemoveControl(GetPlayableLayer(avatar,VRCAvatarDescriptor.AnimLayerType.Action));
            RemoveControl(GetPlayableLayer(avatar, VRCAvatarDescriptor.AnimLayerType.Sitting));

            if (avatar.expressionsMenu)
            {
                for (int i = avatar.expressionsMenu.controls.Count - 1; i >= 0; i--)
                {
                    if (avatar.expressionsMenu.controls[i].name == "Limb Control")
                    {
                        Debug.Log("Removed Limb Control Submenu from Expression Menu");
                        avatar.expressionsMenu.controls.RemoveAt(i);
                        EditorUtility.SetDirty(avatar.expressionsMenu);
                        continue;
                    }
                    if (avatar.expressionsMenu.controls[i].name == "Tracking Control")
                    {
                        Debug.Log("Removed Tracking Control Submenu from Expression Menu");
                        avatar.expressionsMenu.controls.RemoveAt(i);
                        EditorUtility.SetDirty(avatar.expressionsMenu);
                        continue;
                    }
                }
            }
            if (avatar.expressionParameters)
            {
                for (int i=avatar.expressionParameters.parameters.Length-1;i>=0;i--)
                {
                    if (avatar.expressionParameters.parameters[i].name.StartsWith("LC/"))
                    {
                        Debug.Log("Removed " + avatar.expressionParameters.parameters[i].name + " From Expression Parameters");
                        ArrayUtility.RemoveAt(ref avatar.expressionParameters.parameters, i);
                    }
                }
                EditorUtility.SetDirty(avatar.expressionParameters);
            }

            Debug.Log("Finished removing Limb Control!");
        }



        private void OnEnable()
        {
            if (avatar == null) avatar = FindObjectOfType<VRCAvatarDescriptor>();

            assetPath = EditorPrefs.GetString("LimbControlPath", "Assets/DreadScripts/LimbControl/Generated Assets");
            GetBaseTree();
        }

        private static void GetBaseTree()
        {
            if (!customTree || !useCustomTree)
                customTree = Resources.Load<BlendTree>("Animations/LC_NormalBlendTree");
        }

		#region GUI Methods

		private static void DrawColoredButton(ref bool toggle, string label) => DrawColoredButton(ref toggle, new GUIContent(label));
		private static void DrawColoredButton(ref bool toggle, GUIContent label)
		{
			using (new BGColoredScope(toggle))
				toggle = GUILayout.Toggle(toggle, label, EditorStyles.toolbarButton);
		}

		private static void DrawTitle(string title, string tooltip = "")
		{
			using (new GUILayout.HorizontalScope("in bigtitle"))
			{
				bool hasTooltip = !string.IsNullOrEmpty(tooltip);
				if (hasTooltip) GUILayout.Space(21);
                GUILayout.Label(title, Content.styleTitle);
                if (hasTooltip) GUILayout.Label(new GUIContent(Content.iconHelp){tooltip = tooltip}, GUILayout.Width(18));
			}
		}
		private static void Credit()
		{
			using (new GUILayout.HorizontalScope())
			{
				GUILayout.FlexibleSpace();
				if (GUILayout.Button("Made By Dreadrith#3238", "boldlabel"))
					Application.OpenURL("https://linktr.ee/Dreadrith");
			}
		}
		#endregion

		#region DSHelper Methods

		private static AnimatorController GetPlayableLayer(VRCAvatarDescriptor avi, VRCAvatarDescriptor.AnimLayerType type)
        {
            for (var i = 0; i < avi.baseAnimationLayers.Length; i++)
                if (avi.baseAnimationLayers[i].type == type)
                    return GetController(avi.baseAnimationLayers[i].animatorController);

            for (var i = 0; i < avi.specialAnimationLayers.Length; i++)
                if (avi.specialAnimationLayers[i].type == type)
                    return GetController(avi.specialAnimationLayers[i].animatorController);

            return null;
        }
        private static bool SetPlayableLayer(VRCAvatarDescriptor avi, VRCAvatarDescriptor.AnimLayerType type,RuntimeAnimatorController ani)
        {
            for (var i = 0; i < avi.baseAnimationLayers.Length; i++)
                if (avi.baseAnimationLayers[i].type == type)
                {
                    if (ani)
                        avi.customizeAnimationLayers = true;
                    avi.baseAnimationLayers[i].isDefault = !ani;
                    avi.baseAnimationLayers[i].animatorController = ani;
                    EditorUtility.SetDirty(avi);
                    return true;
                }

            for (var i = 0; i < avi.specialAnimationLayers.Length; i++)
                if (avi.specialAnimationLayers[i].type == type)
                {
                    if (ani)
                        avi.customizeAnimationLayers = true;
                    avi.specialAnimationLayers[i].isDefault = !ani;
                    avi.specialAnimationLayers[i].animatorController = ani;
                    EditorUtility.SetDirty(avi);
                    return true;
                }


            return false;
        }
        private static AnimatorController GetController(RuntimeAnimatorController controller)
        {
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(controller));
        }

        private static AnimatorStateTransition InstantTransition(AnimatorState state, AnimatorState destination)
        {
            var t = state.AddTransition(destination, false);
            t.duration = 0;
            return t;
        }
        private static AnimatorStateTransition InstantExitTransition(AnimatorState state)
        {
	        var t = state.AddExitTransition(false);
	        t.duration = 0;
	        return t;
        }

		private static void AddTag(AnimatorControllerLayer layer, string tag)
        {
            if (HasTag(layer, tag)) return;

            var t = layer.stateMachine.AddAnyStateTransition((AnimatorState)null);
            t.isExit = true;
            t.mute = true;
            t.name = tag;
        }
        private static bool HasTag(AnimatorControllerLayer layer, string tag)
        {
            return layer.stateMachine.anyStateTransitions.Any(t => t.isExit && t.mute && t.name == tag);
        }

        private static AnimatorControllerLayer AddLayer(AnimatorController controller, string name, float defaultWeight, AvatarMask mask = null)
        {
            var newLayer = new AnimatorControllerLayer
            {
                name = name,
                defaultWeight = defaultWeight,
                avatarMask = mask,
                stateMachine = new AnimatorStateMachine
                {
                    name = name,
                    hideFlags = HideFlags.HideInHierarchy,
                    entryPosition = new Vector3(50, 120),
                    anyStatePosition = new Vector3(50, 80),
                    exitPosition = new Vector3(50, 40),
                },
            };
            AssetDatabase.AddObjectToAsset(newLayer.stateMachine, controller);
            controller.AddLayer(newLayer);
            return newLayer;
        }
        private static void ReadyParameter(AnimatorController controller, string parameter, AnimatorControllerParameterType type)
        {
            if (!GetParameter(controller, parameter, out _))
                controller.AddParameter(parameter, type);
        }
        private static bool GetParameter(AnimatorController controller, string parameter, out int index)
        {
            index = -1;
            for (int i = 0; i < controller.parameters.Length; i++)
            {
                if (controller.parameters[i].name == parameter)
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }



        private static void ReadyPath(string folderPath)
        {
	        if (!Directory.Exists(folderPath))
	        {
		        Directory.CreateDirectory(folderPath);
		        AssetDatabase.ImportAsset(folderPath);
	        }
        }

		internal static string ValidatePath(string path)
		{
			string regexFolderReplace = Regex.Escape(new string(Path.GetInvalidPathChars()));

			path = path.Replace('\\', '/');
			if (path.IndexOf('/') > 0)
				path = string.Join("/", path.Split('/').Select(s => Regex.Replace(s, $@"[{regexFolderReplace}]", "-")));

			return path;
		}
		internal static string ValidateName(string name)
		{
			string regexFileReplace = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
			return string.IsNullOrEmpty(name) ? "Unnamed" : Regex.Replace(name, $@"[{regexFileReplace}]", "-");
		}


		private static T CopyAssetAndReturn<T>(string path, string newpath) where T : Object
        {
            if (path != newpath)
                AssetDatabase.CopyAsset(path, newpath);
            return AssetDatabase.LoadAssetAtPath<T>(newpath);

        }

		internal static T CopyAssetAndReturn<T>(T obj, string newPath) where T : Object
		{
			string assetPath = AssetDatabase.GetAssetPath(obj);
			Object mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);

			if (!mainAsset) return null;
			if (obj != mainAsset)
			{
				T newAsset = Object.Instantiate(obj);
				AssetDatabase.CreateAsset(newAsset, newPath);
				return newAsset;
			}

			AssetDatabase.CopyAsset(assetPath, newPath);
			return AssetDatabase.LoadAssetAtPath<T>(newPath);
		}


		private static VRCExpressionParameters ReplaceParameters(VRCAvatarDescriptor avi, string folderPath)
        {
            avi.customExpressions = true;
            if (avi.expressionParameters)
            {
                avi.expressionParameters = CopyAssetAndReturn<VRCExpressionParameters>(avi.expressionParameters, AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + avi.expressionParameters.name + ".asset"));
                return avi.expressionParameters;
            }

            var assetPath = folderPath + "/" + avi.gameObject.name + " Parameters.asset";
            var newParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            newParameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();
            AssetDatabase.CreateAsset(newParameters, AssetDatabase.GenerateUniqueAssetPath(assetPath));
            AssetDatabase.ImportAsset(assetPath);
            avi.expressionParameters = newParameters;
            avi.customExpressions = true;
            return newParameters;
        }

        private static VRCExpressionsMenu ReplaceMenu(VRCExpressionsMenu menu, string folderPath, bool deep = true, Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> copyDict = null)
        {
            VRCExpressionsMenu newMenu;
            if (!menu)
                return null;
            if (copyDict == null)
                copyDict = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();
            
            if (copyDict.ContainsKey(menu))
                newMenu = copyDict[menu];
            else
            {
                newMenu = CopyAssetAndReturn<VRCExpressionsMenu>(menu, AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + menu.name + ".asset"));
                copyDict.Add(menu, newMenu);
                if (!deep) return newMenu;

                foreach (var c in newMenu.controls.Where(c => c.type == VRCExpressionsMenu.Control.ControlType.SubMenu && c.subMenu != null))
                {
                    c.subMenu = ReplaceMenu(c.subMenu, folderPath, true, copyDict);
                }

                EditorUtility.SetDirty(newMenu);
            }
            return newMenu;
        }

        private static VRCExpressionsMenu ReplaceMenu(VRCAvatarDescriptor avi, string folderPath, bool deep = false)
        {
            avi.customExpressions = true;
            if (avi.expressionsMenu)
            {
                avi.expressionsMenu = ReplaceMenu(avi.expressionsMenu, folderPath, deep);
                return avi.expressionsMenu;
            }


            var newMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            newMenu.controls = new List<VRCExpressionsMenu.Control>();
            AssetDatabase.CreateAsset(newMenu, AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + avi.gameObject.name + " Menu.asset"));
            avi.expressionsMenu = newMenu;
            avi.customExpressions = true;
            return newMenu;

        }
        
        private static void AddControls(VRCExpressionsMenu target, List<VRCExpressionsMenu.Control> newCons)
        {
            foreach (var c in newCons)
                target.controls.Add(c);
            
            EditorUtility.SetDirty(target);
        }

        private static void AddParameters(VRCExpressionParameters target, List<VRCExpressionParameters.Parameter> newParams)
        {
            target.parameters = target.parameters == null || target.parameters.Length <= 0 ? newParams.ToArray() : target.parameters.Concat(newParams).ToArray();
            
            EditorUtility.SetDirty(target);
            AssetDatabase.WriteImportSettingsIfDirty(AssetDatabase.GetAssetPath(target));
        }
        private static string GenerateUniqueString(string s, System.Func<string, bool> check)
        {
            if (check(s))
                return s;

            int suffix = 0;

            int.TryParse(s.Substring(s.Length - 2, 2), out int d);
            if (d >= 0)
                suffix = d;
            if (suffix > 0) s = suffix > 9 ? s.Substring(0, s.Length - 2) : s.Substring(0, s.Length - 1);

            s = s.Trim();

            suffix++;

            string newString = s + " " + suffix;
            while (!check(newString))
            {
                suffix++;
                newString = s + " " + suffix;
            }

            return newString;
        }
        #endregion

        internal static class CustomGUI
        {
	        internal static class Content
	        {
		        internal static GUIContent iconTrash = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Trash")) { tooltip = "Remove Limb Control from Avatar" };
		        internal static GUIContent iconError = new GUIContent(EditorGUIUtility.IconContent("console.warnicon.sml")) { tooltip = "Not enough memory available in Expression Parameters!" };
		        internal static GUIContent iconHelp = new GUIContent(EditorGUIUtility.IconContent("UnityEditor.InspectorWindow"));

				internal static GUIContent
			        customTreeContent = new GUIContent("Use Custom BlendTree", "Set a custom BlendTree to change the way the limbs move"),
			        avatarContent = new GUIContent("Avatar Descriptor", "Drag and Drop your avatar here."),
			        trackingContent = new GUIContent("Add Tracking Control", "Add a SubMenu that allows the individual Enable/Disable of limb tracking"),
			        sameControlContent = new GUIContent("Use Same Control", "Selected limbs will be controlled together using the same single control");

		        internal static GUIStyle styleTitle = new GUIStyle(GUI.skin.label) {alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold};
		        internal static GUIStyle comicallyLargeButton = new GUIStyle(GUI.skin.button) {fontSize = 16, fontStyle = FontStyle.Bold};
	        }

			internal sealed class BGColoredScope : System.IDisposable
			{
				private readonly Color ogColor;
				public BGColoredScope(Color setColor)
				{
					ogColor = GUI.backgroundColor;
					GUI.backgroundColor = setColor;
				}

				public BGColoredScope(bool isActive)
				{
					ogColor = GUI.backgroundColor;
					GUI.backgroundColor = isActive ? Color.green : Color.grey;
				}

				public void Dispose() =>	GUI.backgroundColor = ogColor;
				
			}

			internal static string AssetFolderPath(string variable, string title, string prefKey)
			{
				using (new GUILayout.HorizontalScope())
				{
					using (new EditorGUI.DisabledScope(true))
						EditorGUILayout.TextField(title, variable);

					if (GUILayout.Button("...", GUILayout.Width(30)))
					{
						var dummyPath = EditorUtility.OpenFolderPanel(title, AssetDatabase.IsValidFolder(variable) ? variable : "Assets", string.Empty);
						if (string.IsNullOrEmpty(dummyPath))
							return variable;
						string newPath = FileUtil.GetProjectRelativePath(dummyPath);

						if (!newPath.StartsWith("Assets"))
						{
							Debug.LogWarning("New Path must be a folder within Assets!");
							return variable;
						}

						newPath = ValidatePath(newPath);
						variable = newPath;
						EditorPrefs.SetString(prefKey, variable);
					}
				}

				return variable;
			}

			internal static void DrawSeparator(int thickness = 2, int padding = 10)
			{
				Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(thickness + padding));
				r.height = thickness;
				r.y += padding / 2f;
				r.x -= 2;
				r.width += 6;
				ColorUtility.TryParseHtmlString(EditorGUIUtility.isProSkin ? "#595959" : "#858585", out Color lineColor);
				EditorGUI.DrawRect(r, lineColor);
			}
		}
	}
}
