using UnityEngine;

namespace HarmonicOrbits
{
    /// <summary>Entry point; loads once at startup and survives scene changes.</summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class HarmonicOrbits : MonoBehaviour
    {
        public static HarmonicOrbits Singleton;

        // ReSharper disable once UnusedMember.Global
        public void Awake()
        {
            Singleton = this;
            DontDestroyOnLoad(this);

            //Scaffold marker; the toolchain is verified when this reaches KSP.log. Remove once
            //the addon does real work, per the log-failures-only rule.
            Debug.Log("[HarmonicOrbits]: loaded");
        }
    }
}
