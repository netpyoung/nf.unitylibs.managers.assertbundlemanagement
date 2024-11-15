using NF.UnityLibs.Managers.AssetBundleManagement.Impl;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NF.UnityLibs.Managers.AssetBundleManagement
{
    [Serializable]
    public class AssetBundleManager : IDisposable
    {
        private bool _isDisposed = false;
        private HashSet<string> _assetBundleNameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [SerializeField]
        private AssetBundle? _ab_AssetBundleManifestOrNull = null;

        [SerializeField]
        private TaskQueueProcessor _taskQueueProcessor = new TaskQueueProcessor();

        [SerializeField]
        internal BundleFactory _bundleFactory = new BundleFactory();

        private CancellationTokenSource _cts = new CancellationTokenSource();
        private CancellationToken _ct = CancellationToken.None;

        public void Dispose()
        {
            Assert.IsFalse(_isDisposed, "disposed");
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _taskQueueProcessor.Dispose();
            _bundleFactory.Dispose();

            if (_ab_AssetBundleManifestOrNull != null)
            {
                _ab_AssetBundleManifestOrNull.Unload(true);
                _ab_AssetBundleManifestOrNull = null;
            }

            _assetBundleNameSet.Clear();
        }

        public virtual async Task<Exception?> InitAsync(string deviceBaseDirFpath, string manifestName)
        {
            Assert.IsFalse(_isDisposed, "disposed");

            string ab_AssetBundleManifestFpath = $"{deviceBaseDirFpath}/{manifestName}";
            if (!File.Exists(ab_AssetBundleManifestFpath))
            {
                return new AssetBundleManagerException(E_EXCEPTION_KIND.ERR_ON_INITIALIZE, $"!File.Exists({ab_AssetBundleManifestFpath})");
            }

            AssetBundleCreateRequest abcr = AssetBundle.LoadFromFileAsync(ab_AssetBundleManifestFpath);
            while (!abcr.isDone)
            {
                if (_ct.IsCancellationRequested)
                {
                    return new AssetBundleManagerException(E_EXCEPTION_KIND.ERR_ON_INITIALIZE, $"_ct.IsCancellationRequested");
                }
                await Task.Yield();
            }

            AssetBundle? ab_AssetBundleManifestOrNull = abcr.assetBundle;
            if (ab_AssetBundleManifestOrNull is null)
            {
                return new AssetBundleManagerException(E_EXCEPTION_KIND.ERR_ON_INITIALIZE, $"ab_AssetBundleManifestOrNull is null | {ab_AssetBundleManifestFpath}");
            }

            AssetBundleManifest? manifestOrNull = ab_AssetBundleManifestOrNull.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            if (manifestOrNull is null)
            {
                return new AssetBundleManagerException(E_EXCEPTION_KIND.ERR_ON_INITIALIZE, $"manifestOrNull is null | {ab_AssetBundleManifestFpath}");
            }

            _ab_AssetBundleManifestOrNull = ab_AssetBundleManifestOrNull;

            _assetBundleNameSet.Clear();
            string[] bundleNames = manifestOrNull.GetAllAssetBundles();
            foreach (string bundleName in bundleNames)
            {
                _assetBundleNameSet.Add(bundleName);
            }

            _taskQueueProcessor.Init(deviceBaseDirFpath, manifestOrNull!);
            _ct = _cts.Token;
            return null;
        }

        public virtual TaskBundle<T>? RentBundleOrNull<T>(string assetBundleName) where T : Object
        {
            Assert.IsFalse(_isDisposed, "disposed");

            if (!_assetBundleNameSet.Contains(assetBundleName))
            {
                Debug.LogError($"!_assetBundleNameSet.Contains(\"{assetBundleName}\")");
                return null;
            }

            TaskBundle<T> ret = new TaskBundle<T>(_taskQueueProcessor, _bundleFactory, assetBundleName, _ct);
            return ret;
        }

        public virtual TaskBundleScene? RentBundleSceneOrNull(string assetBundleName)
        {
            Assert.IsFalse(_isDisposed, "disposed");

            if (!_assetBundleNameSet.Contains(assetBundleName))
            {
                Debug.LogError($"!_assetBundleNameSet.Contains(\"{assetBundleName}\")");
                return null;
            }

            TaskBundleScene ret = new TaskBundleScene(_taskQueueProcessor, _bundleFactory, assetBundleName, _ct);
            return ret;
        }

        public virtual async Task ReturnBundleAsync(Bundle bundle)
        {
            Assert.IsFalse(_isDisposed, "disposed");

            if (bundle.State != Bundle.E_BUNDLE_STATE.RENTED)
            {
                Debug.LogError($"bundle.State != Bundle.E_BUNDLE_STATE.RENTED: {bundle.State}");
                return;
            }

            _bundleFactory.PrepareReturn(bundle);
            await _taskQueueProcessor.EnqueueTaskBundleUnload(bundle);
        }

        public void Update()
        {
            Assert.IsFalse(_isDisposed, "disposed");

            _taskQueueProcessor.Update();
        }
    }
}