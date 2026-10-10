using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MaykerStudio.Demo
{

    public class ParticleAnimationEvent : MonoBehaviour
    {
        [SerializeField]
        private Transform particleContainer;

        /// <summary>
        /// 설정한 컨테이너에서 현재 선택한 베기 파티클을 찾아 재생한다.
        /// 투사체일 때는 데모 컨트롤러에 발사를 요청하며 빈 컨테이너에서는 요청하지 않는다.
        /// </summary>
        public void PlayParticle()
        {
            Transform container = particleContainer != null ? particleContainer : transform;
            ParticleSystem particleSystem = container.GetComponentInChildren<ParticleSystem>();
            if (particleSystem == null)
                return;

            if (particleSystem && !particleSystem.GetComponent<Projectile>())
            {
                particleSystem.Play(true);
            }
            else if (AttackerController.Instance != null)
            {
                AttackerController.Instance.SpawnProjectile();
            }
        }
    }
}
