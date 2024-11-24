using System;
using UnityEngine;

namespace Valve.VR
{
    [Serializable]
    public abstract class SteamVR_Action<SourceMap, SourceElement> : SteamVR_Action, ISteamVR_Action, ISteamVR_Action_Source
        where SourceMap : SteamVR_Action_Source_Map<SourceElement>, new() where SourceElement : SteamVR_Action_Source, new()
    {
        [NonSerialized] protected SourceMap sourceMap;

        [NonSerialized] protected bool initialized;

        public virtual SourceElement this[SteamVR_Input_Sources inputSource] => sourceMap[inputSource];

        public override string fullPath => sourceMap.fullPath;

        public override ulong handle => sourceMap.handle;

        public override SteamVR_ActionSet actionSet => sourceMap.actionSet;

        public override SteamVR_ActionDirections direction => sourceMap.direction;

        public override bool active => sourceMap[SteamVR_Input_Sources.Any].active;

        public override bool lastActive => sourceMap[SteamVR_Input_Sources.Any].lastActive;

        public override bool activeBinding => sourceMap[SteamVR_Input_Sources.Any].activeBinding;

        public override bool lastActiveBinding => sourceMap[SteamVR_Input_Sources.Any].lastActiveBinding;

        public override void PreInitialize(string newActionPath)
        {
            actionPath = newActionPath;
            sourceMap = new SourceMap();
            sourceMap.PreInitialize(this, actionPath);
            initialized = true;
        }

        protected override void CreateUninitialized(string newActionPath, bool caseSensitive)
        {
            actionPath = newActionPath;
            sourceMap = new SourceMap();
            sourceMap.PreInitialize(this, actionPath, false);
            needsReinit = true;
            initialized = false;
        }

        protected override void CreateUninitialized(string newActionSet, SteamVR_ActionDirections direction, string newAction, bool caseSensitive)
        {
            actionPath = SteamVR_Input_ActionFile_Action.CreateNewName(newActionSet, direction, newAction);
            sourceMap = new SourceMap();
            sourceMap.PreInitialize(this, actionPath, false);
            needsReinit = true;
            initialized = false;
        }

        public override string TryNeedsInitData()
        {
            if (needsReinit && actionPath != null)
            {
                var steamVR_Action = FindExistingActionForPartialPath(actionPath);
                if (!(steamVR_Action == null))
                {
                    actionPath = steamVR_Action.fullPath;
                    sourceMap = (SourceMap)steamVR_Action.GetSourceMap();
                    initialized = true;
                    needsReinit = false;
                    return actionPath;
                }

                sourceMap = null;
            }

            return null;
        }

        public override void Initialize(bool createNew = false, bool throwErrors = true)
        {
            if (needsReinit) TryNeedsInitData();
            if (createNew)
                sourceMap.Initialize();
            else
            {
                sourceMap = SteamVR_Input.GetActionDataFromPath<SourceMap>(actionPath);
                _ = sourceMap;
            }

            initialized = true;
        }

        public override SteamVR_Action_Source_Map GetSourceMap()
        {
            return sourceMap;
        }

        protected override void InitializeCopy(string newActionPath, SteamVR_Action_Source_Map newData)
        {
            actionPath = newActionPath;
            sourceMap = (SourceMap)newData;
            initialized = true;
        }

        protected void InitAfterDeserialize()
        {
            if (sourceMap != null)
            {
                if (sourceMap.fullPath != actionPath)
                {
                    needsReinit = true;
                    TryNeedsInitData();
                }

                if (string.IsNullOrEmpty(actionPath)) sourceMap = null;
            }

            if (!initialized) Initialize(false, false);
        }

        public override bool GetActive(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].active;
        }

        public override bool GetActiveBinding(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].activeBinding;
        }

        public override bool GetLastActive(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].lastActive;
        }

        public override bool GetLastActiveBinding(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].lastActiveBinding;
        }
    }

    [Serializable]
    public abstract class SteamVR_Action : IEquatable<SteamVR_Action>, ISteamVR_Action, ISteamVR_Action_Source
    {
        [SerializeField] protected string actionPath;

        [SerializeField] protected bool needsReinit;

        public static bool startUpdatingSourceOnAccess = true;

        [NonSerialized] private string cachedShortName;

        public abstract string fullPath { get; }

        public abstract ulong handle { get; }

        public abstract SteamVR_ActionSet actionSet { get; }

        public abstract SteamVR_ActionDirections direction { get; }

        public bool setActive => actionSet.IsActive(SteamVR_Input_Sources.Any);

        public abstract bool active { get; }

        public abstract bool activeBinding { get; }

        public abstract bool lastActive { get; }

        public abstract bool lastActiveBinding { get; }

        public SteamVR_Action() { }

        public static CreateType Create<CreateType>(string newActionPath) where CreateType : SteamVR_Action, new()
        {
            var val = new CreateType();
            val.PreInitialize(newActionPath);
            return val;
        }

        public static CreateType CreateUninitialized<CreateType>(string setName, SteamVR_ActionDirections direction, string newActionName, bool caseSensitive) where CreateType : SteamVR_Action, new()
        {
            var val = new CreateType();
            val.CreateUninitialized(setName, direction, newActionName, caseSensitive);
            return val;
        }

        public static CreateType CreateUninitialized<CreateType>(string actionPath, bool caseSensitive) where CreateType : SteamVR_Action, new()
        {
            var val = new CreateType();
            val.CreateUninitialized(actionPath, caseSensitive);
            return val;
        }

        public CreateType GetCopy<CreateType>() where CreateType : SteamVR_Action, new()
        {
            if (SteamVR_Input.ShouldMakeCopy())
            {
                var val = new CreateType();
                val.InitializeCopy(actionPath, GetSourceMap());
                return val;
            }

            return (CreateType)this;
        }

        public abstract string TryNeedsInitData();

        protected abstract void InitializeCopy(string newActionPath, SteamVR_Action_Source_Map newData);

        public abstract void PreInitialize(string newActionPath);

        protected abstract void CreateUninitialized(string newActionPath, bool caseSensitive);

        protected abstract void CreateUninitialized(string newActionSet, SteamVR_ActionDirections direction, string newAction, bool caseSensitive);

        public abstract void Initialize(bool createNew = false, bool throwNotSetError = true);

        public abstract float GetTimeLastChanged(SteamVR_Input_Sources inputSource);

        public abstract SteamVR_Action_Source_Map GetSourceMap();

        public abstract bool GetActive(SteamVR_Input_Sources inputSource);

        public bool GetSetActive(SteamVR_Input_Sources inputSource)
        {
            return actionSet.IsActive(inputSource);
        }

        public abstract bool GetActiveBinding(SteamVR_Input_Sources inputSource);

        public abstract bool GetLastActive(SteamVR_Input_Sources inputSource);

        public abstract bool GetLastActiveBinding(SteamVR_Input_Sources inputSource);

        public string GetPath()
        {
            return actionPath;
        }

        public abstract bool IsUpdating(SteamVR_Input_Sources inputSource);

        public override int GetHashCode()
        {
            if (actionPath == null) return 0;
            return actionPath.GetHashCode();
        }

        public bool Equals(SteamVR_Action other)
        {
            if ((object)other == null) return false;
            return actionPath == other.actionPath;
        }

        public override bool Equals(object other)
        {
            if (other == null)
            {
                if (string.IsNullOrEmpty(actionPath)) return true;
                if (GetSourceMap() == null) return true;
                return false;
            }

            if (this == other) return true;
            if (other is SteamVR_Action) return Equals((SteamVR_Action)other);
            return false;
        }

        public static bool operator !=(SteamVR_Action action1, SteamVR_Action action2)
        {
            return !(action1 == action2);
        }

        public static bool operator ==(SteamVR_Action action1, SteamVR_Action action2)
        {
            var flag = (object)action1 == null || string.IsNullOrEmpty(action1.actionPath) || action1.GetSourceMap() == null;
            var flag2 = (object)action2 == null || string.IsNullOrEmpty(action2.actionPath) || action2.GetSourceMap() == null;
            if (flag && flag2) return true;
            if (flag != flag2) return false;
            return action1.Equals(action2);
        }

        public static SteamVR_Action FindExistingActionForPartialPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.IndexOf('/') == -1) return null;
            var array = path.Split('/');
            if (array.Length >= 5 && string.IsNullOrEmpty(array[2]))
            {
                var actionSetName = array[2];
                var actionName = array[4];
                return SteamVR_Input.GetBaseAction(actionSetName, actionName);
            }

            return SteamVR_Input.GetBaseActionFromPath(path);
        }

        public string GetShortName()
        {
            if (cachedShortName == null) cachedShortName = SteamVR_Input_ActionFile.GetShortName(fullPath);
            return cachedShortName;
        }

        public void ShowOrigins()
        {
            OpenVR.Input.ShowActionOrigins(actionSet.handle, handle);
        }

        public void HideOrigins()
        {
            OpenVR.Input.ShowActionOrigins(0uL, 0uL);
        }
    }
}
