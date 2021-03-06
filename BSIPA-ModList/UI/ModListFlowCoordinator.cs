﻿using BSIPA_ModList.UI.ViewControllers;
using CustomUI.BeatSaber;
using CustomUI.Utilities;
using IPA.Loader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using VRUI;

namespace BSIPA_ModList.UI
{
    internal class ModListFlowCoordinator : FlowCoordinator
    {
        private BackButtonNavigationController navigationController;
        private ModListController modList;
        private DownloadProgressViewController downloads;
        private VRUIViewController settings;

#pragma warning disable CS0618
        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        { // thx Caeden
            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                title = "Installed Mods";

                navigationController = BeatSaberUI.CreateViewController<BackButtonNavigationController>();
                navigationController.didFinishEvent += backButton_DidFinish;

                modList = BeatSaberUI.CreateViewController<ModListController>();
                modList.Init(this, PluginManager.AllPlugins.Select(p => p.Metadata).Concat(PluginManager.DisabledPlugins), PluginLoader.ignoredPlugins, PluginManager.Plugins);

                settings = SettingsViewController.Create();

                downloads = BeatSaberUI.CreateViewController<DownloadProgressViewController>();

                PushViewControllerToNavigationController(navigationController, modList);
            }

            ProvideInitialViewControllers(navigationController, settings, downloads);
        }
#pragma warning restore

        private delegate void PresentFlowCoordDel(FlowCoordinator self, FlowCoordinator newF, Action finished, bool immediate, bool replaceTop);
        private static PresentFlowCoordDel presentFlow;

        public void Present(Action finished = null, bool immediate = false, bool replaceTop = false)
        {
            if (presentFlow == null)
            {
                var ty = typeof(FlowCoordinator);
                var m = ty.GetMethod("PresentFlowCoordinator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                presentFlow = (PresentFlowCoordDel)Delegate.CreateDelegate(typeof(PresentFlowCoordDel), m);
            }

            MainFlowCoordinator mainFlow = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();
            presentFlow(mainFlow, this, finished, immediate, replaceTop);
        }

        public bool HasSelected { get; private set; } = false;

        public void SetSelected(VRUIViewController selected, Action callback = null, bool immediate = false)
        {
            if (immediate)
            {
                if (HasSelected)
                    PopViewController(immediate: true);
                PushViewController(selected, callback, true);
                HasSelected = true;
            }
            else
            {
                if (HasSelected)
                    PopViewController(() =>
                    {
                        PushViewController(selected, callback, immediate);
                        HasSelected = true;
                    }, immediate);
                else
                {
                    PushViewController(selected, callback, immediate);
                    HasSelected = true;
                }
            }
        }

        public void ClearSelected(Action callback = null, bool immediate = false)
        {
            if (HasSelected) PopViewController(callback, immediate);
            HasSelected = false;
        }

        public void PushViewController(VRUIViewController controller, Action callback = null, bool immediate = false)
        {
            PushViewControllerToNavigationController(navigationController, controller, callback, immediate);
        }

        public void PopViewController(Action callback = null, bool immediate = false)
        {
            PopViewControllerFromNavigationController(navigationController, callback, immediate);
        }

        internal HashSet<Action> exitActions = new HashSet<Action>();

        private delegate void DismissFlowDel(FlowCoordinator self, FlowCoordinator newF, Action finished, bool immediate);
        private static DismissFlowDel dismissFlow;

        private void backButton_DidFinish()
        {
            if (dismissFlow == null)
            {
                var ty = typeof(FlowCoordinator);
                var m = ty.GetMethod("DismissFlowCoordinator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                dismissFlow = (DismissFlowDel)Delegate.CreateDelegate(typeof(DismissFlowDel), m);
            }

            if (exitActions.Count == 0)
            {
                MainFlowCoordinator mainFlow = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();
                dismissFlow(mainFlow, this, null, false);
            }
            else
            {
                var actions = exitActions;
                UnityAction<Scene, LoadSceneMode> releaseAction = null;
                releaseAction = (a, b) => SceneManager_sceneLoaded(actions, releaseAction, a, b);
                SceneManager.sceneLoaded += releaseAction;
                Resources.FindObjectsOfTypeAll<MenuTransitionsHelperSO>().First().RestartGame(true);
            }
        }

        private static void SceneManager_sceneLoaded(HashSet<Action> actions, UnityAction<Scene, LoadSceneMode> self, Scene arg0, LoadSceneMode arg1)
        {
            if (arg0.name == "Init")
            {
                SceneManager.sceneLoaded -= self;

                foreach (var act in actions)
                    act();
            }
        }
    }
}
