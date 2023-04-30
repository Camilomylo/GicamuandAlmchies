using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;

namespace Code_Boses
{

    [CreateAssetMenu(menuName = "State_JumpATK")]
    public class State_JumpATK : BaseState
    {
        private enum JumpingState { CalculateJumping, Jumping, Waiting, CalculateLanding, Landing }
        private Vector2 pointA , pointB ;
        [SerializeField] float timeBetweenJumps;
        private float _timer;
        [SerializeField] int numbersOfJumps;
        private int _count;
        private JumpingState _currentState;
        [SerializeField] float clampingCeiling;
        [SerializeField] float clampingFloor;
        private Vector3 _movingTo;
        private float _elapsedTime;
        [SerializeField] float movingDuration;
        [SerializeField] AnimationCurve movingBehaviour;
        private Vector3 _currentBossPosition;
        public override void EnterState(BossStateManager boss)
        {
            _timer = timeBetweenJumps;
            _count = 0;
            _currentState = JumpingState.CalculateJumping;
            _elapsedTime = 0;
        }

        public override void UpdateState(BossStateManager boss)
        {
            
            switch (_currentState) 
            {
                case JumpingState.CalculateJumping:

                    Vector2 moveUp = boss.transform.position;
                    float y = Mathf.Clamp(moveUp.y + 10, clampingFloor, clampingCeiling);
                    _movingTo = new Vector3(moveUp.x, y);
                    _count++;
                    _currentBossPosition = boss.transform.position;
                    _elapsedTime = 0;
                    _currentState = JumpingState.Jumping;

                    break;

                case JumpingState.Jumping:

                    _elapsedTime += Time.deltaTime;
                    float percentageComplete = _elapsedTime / movingDuration;
                    boss.transform.position = Vector3.Lerp(_currentBossPosition, _movingTo, movingBehaviour.Evaluate(percentageComplete));

                    if (boss.transform.position == _movingTo)
                    {
                        
                        _currentState = JumpingState.Waiting;
                    }

                    break;

                    case JumpingState.Waiting:

                    if (_timer <= 0)
                    {
                        _timer = timeBetweenJumps;
                        _currentState = JumpingState.CalculateLanding;
                    }
                    else
                    {
                        _timer -= Time.deltaTime;
                    }

                    break;

                case JumpingState.CalculateLanding:

                    Vector3 playerPos = boss.GetClosestPlayer().transform.position;
                    float playerY = Mathf.Clamp(playerPos.y + 10, clampingFloor, clampingCeiling);
                    Vector3 newPosition = new Vector3(playerPos.x, playerY);
                    boss.transform.position = newPosition;
                    float newY = Mathf.Clamp(boss.transform.position.y - 10,clampingFloor, clampingCeiling);
                    _movingTo = new Vector3(boss.transform.position.x, newY);
                    _currentBossPosition = boss.transform.position;
                    _elapsedTime = 0;
                    _currentState = JumpingState.Landing;

                    break;

                 case JumpingState.Landing:

                    _elapsedTime += Time.deltaTime;
                    float percentageCompleteLanding = _elapsedTime / movingDuration;
                    boss.transform.position = Vector3.Lerp(_currentBossPosition, _movingTo, movingBehaviour.Evaluate(percentageCompleteLanding));

                    if (boss.transform.position == _movingTo)
                    {
                        if (_count == numbersOfJumps)
                        {
                            boss.SwichState(boss.idle);
                        }
                        else
                        {
                            _currentState= JumpingState.CalculateJumping;
                        }
                    }

                    break;
            }
            
        }
    }
}
