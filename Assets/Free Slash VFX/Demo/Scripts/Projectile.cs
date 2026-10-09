using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MaykerStudio.Demo
{
    public class Projectile : MonoBehaviour
    {
        [SerializeField]
        private GameObject SpawnWhenFinish;

        public float speed = 10;
        public float distance = 30;
        private ParticleSystem mainParticle;

        private Vector3 initPosition;
        private bool _fired;
        private bool _finished;

        /// <summary>
        /// 루트 파티클을 초기화하고 현재 위치에서 투사체 비행을 시작한다.
        /// 초기 위치와 비행 상태를 저장하며 모든 자식 파티클을 재생한다.
        /// </summary>
        public void Fire()
        {
            mainParticle = GetComponent<ParticleSystem>();

            initPosition = transform.position;
            _fired = true;
            _finished = false;
            mainParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            mainParticle.Play(true);
        }

        void Update()
        {
            if (!_fired || !mainParticle)
                return;
            if (!_finished && mainParticle.isPlaying)
            {
                transform.position = Vector3.MoveTowards(transform.position, transform.position + transform.forward, speed * Time.deltaTime);

                if (Vector3.Distance(initPosition, transform.position) > distance)
                {
                    mainParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    _finished = true;
                    if (SpawnWhenFinish != null)
                    {
                        Instantiate(SpawnWhenFinish, transform.position, Quaternion.identity);
                    }
                }
            }
            if (!mainParticle.IsAlive(true))
                Destroy(gameObject);
        }
    }
}
