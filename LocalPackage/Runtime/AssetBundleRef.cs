﻿using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace NF.UnityLibs.Managers.AssetBundleManagement
{
    [Serializable]
    public sealed class AssetBundleRef
    {
        public enum E_ASSETBUNDLEREF_STATE
        {
            LOADING,
            LOADED,
            UNLOADED,
        }

        [SerializeField]
        private int _refCount = 0;

        [SerializeField]
        internal Object[] _unityObjects = Array.Empty<Object>();

        [SerializeField]
        private AssetBundle? _assetBundleOrNull = null;

        [SerializeField]
        private string _name;

        [SerializeField]
        private string[] _dependencies;

        [SerializeField]
        internal E_ASSETBUNDLEREF_STATE _state;

        private AssetBundleCreateRequest? _assetBundleCreateRequest;
        private AssetBundleRequest? _assetBundleRequest;
        public AssetBundle? AssetBundleOrNull => _assetBundleOrNull;
        public string Name => _name;
        public string[] Dependencies => _dependencies;
        public E_ASSETBUNDLEREF_STATE State => _state;

        public Object[] UnityObjects
        {
            get
            {
                Assert.AreEqual(E_ASSETBUNDLEREF_STATE.LOADED, _state, $"_state should be LOADED : {_state}");
                return _unityObjects;
            }
        }

        public float Progress
        {
            get
            {
                if (_assetBundleOrNull is not null)
                {
                    return 1;
                }

                if (_assetBundleCreateRequest is null)
                {
                    return 0;
                }

                float progress = _assetBundleCreateRequest.progress / 2.0f;
                if (_assetBundleRequest is not null)
                {
                    progress += _assetBundleRequest.progress / 2.0f;
                }

                return progress;
            }
        }

        internal AssetBundleRef(string name, string[] dependencies)
        {
            _name = name;
            _dependencies = dependencies;
            _state = E_ASSETBUNDLEREF_STATE.LOADING;
            ReferenceCount_Increment();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int ReferenceCount_Increment()
        {
            Assert.AreNotEqual(E_ASSETBUNDLEREF_STATE.UNLOADED, _state, $"ReferenceCount_Increment: {_state}");
            return ++_refCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int ReferenceCount_Decrement()
        {
            Assert.AreEqual(E_ASSETBUNDLEREF_STATE.LOADED, _state, $"ReferenceCount_Decrement: {_state}");
            return --_refCount;
        }

        internal void Unload()
        {
            Assert.AreEqual(E_ASSETBUNDLEREF_STATE.LOADED, _state, $"Unload: {_state}");
            if (_assetBundleOrNull is not null)
            {
                _assetBundleOrNull.Unload(true);
                _assetBundleOrNull = null;
            }

            _state = E_ASSETBUNDLEREF_STATE.UNLOADED;
        }

        internal void SetAssetBundle(AssetBundle bundle)
        {
            Assert.AreEqual(E_ASSETBUNDLEREF_STATE.LOADING, _state, $"SetAssetBundle: {_state}");

            if (bundle.isStreamedSceneAssetBundle)
            {
                _assetBundleOrNull = bundle;
                _state = E_ASSETBUNDLEREF_STATE.LOADED;
                return;
            }

            _assetBundleOrNull = bundle;
            AssetBundleRequest abr = bundle.LoadAllAssetsAsync();
            abr.completed += (AsyncOperation ao) =>
            {
                Object[]? unityObjectsOrNull = abr.allAssets;
                Assert.IsNotNull(unityObjectsOrNull, $"unityObjectsOrNull is null : {bundle.name}");

                _unityObjects = unityObjectsOrNull!;
                _state = E_ASSETBUNDLEREF_STATE.LOADED;
            };

            _assetBundleRequest = abr;
        }

        internal void LoadAssetBundleRequest(string assetBundleFpath, Action<AssetBundle> act)
        {
            AssetBundleCreateRequest abcr = AssetBundle.LoadFromFileAsync(assetBundleFpath);
            abcr.completed += (AsyncOperation ao) =>
            {
                AssetBundle bundle = abcr.assetBundle;
                Assert.IsNotNull(bundle, $"abcr.assetBundle is null. {_name}, {assetBundleFpath}");
                act(bundle);
            };
            _assetBundleCreateRequest = abcr;
        }
    }
}