using UnityEngine;
using System.Collections;

namespace EpicToonFX
{
    public class ETFXLightFade : MonoBehaviour
    {
        [Header("Seconds to dim the light")]
        public float life = 0.2f;
        public bool killAfterLife = true;

        private Light li;
        private float initIntensity;

        // Use this for initialization
        void Start()
        {
            if (gameObject.GetComponent<Light>())
            {
                li = gameObject.GetComponent<Light>();
                initIntensity = li.intensity;
            }
            else
                print("No light object found on " + gameObject.name);
        }

        // Update is called once per frame
        void Update()
        {
            if (li != null && li.enabled)
            {
                li.intensity = Mathf.Max(0f, li.intensity - initIntensity * (Time.deltaTime / Mathf.Max(0.01f, life)));
                if (killAfterLife && li.intensity <= 0)
                {
                    li.enabled = false;
                    enabled = false;
                }
            }
        }
    }
}
