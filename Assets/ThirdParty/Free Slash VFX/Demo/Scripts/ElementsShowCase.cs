using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MaykerStudio.Demo
{

    public class ElementsShowCase : MonoBehaviour
    {
        public ParticleSystem[] particles;

        private Coroutine coroutine;

        /// <summary>
        /// 등록한 파티클을 정지·대기·재생 순서로 반복한다.
        /// 외부 입력 없이 코루틴이 취소될 때까지 데모의 반복 표시를 유지한다.
        /// </summary>
        private IEnumerator StartParts()
        {
            while (true)
            {
                foreach (ParticleSystem particle in particles)
                {
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }

                yield return new WaitForSeconds(1f);

                foreach (ParticleSystem particle in particles)
                {
                    particle.Play(true);
                }

                yield return new WaitForSeconds(1.5f);
            }
        }

        /// <summary>
        /// 반복 코루틴과 등록된 파티클의 추가 방출을 중단한다.
        /// 코루틴 참조를 해제하고 현재 입자는 자연 종료하게 둔다.
        /// </summary>
        public void StopAllParticles()
        {
            if (coroutine != null)
                StopCoroutine(coroutine);

            coroutine = null;

            foreach (ParticleSystem particle in particles)
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>
        /// 등록한 파티클의 반복 시연을 시작한다.
        /// 기존 코루틴을 정리해 재호출에도 반복 작업이 하나만 실행되게 한다.
        /// </summary>
        public void StartAllParticles()
        {
            StopAllParticles();
            foreach (ParticleSystem particle in particles)
            {
                particle.Play(true);
            }

            coroutine = StartCoroutine(StartParts());
        }

        void OnDisable()
        {
            StopAllParticles();
        }
    }

}
