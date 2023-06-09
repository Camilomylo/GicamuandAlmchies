using Code_Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using TarodevController;
using Code;
using Code_DungeonSystem;

public class PlayerController : MonoBehaviour
{
    public Vector3 Velocity { get; private set; }
    public FrameInput Input { get; protected set; }
    public bool JumpingThisFrame { get; private set; }
    public bool LandingThisFrame { get; private set; }
    public Vector3 RawMovement { get; private set; }
    public bool Grounded => _colDown;

    public bool pauseControllers = false;

    private GameObject footsteps;

    [SerializeField] public int currentRoom = 0;
    [SerializeField] public int bossRoom;

    private Vector3 _lastPosition;
    private float _currentHorizontalSpeed, _currentVerticalSpeed;
    [SerializeField] protected Transform launchPosition;
    public int RP;

    public static event Action OnChangeLife, OnWining, OnLosing;
    public static event Action<int> CoolDown;
    private void Start()
    {
        SetUp();
        footsteps = Instantiate(new GameObject(),transform.position,Quaternion.identity);
        AudioSource a = footsteps.AddComponent<AudioSource>();
        Sound s = AudioManager.instance.GetSound(1);
        a.clip = s.clip;
        a.pitch= s.pitch;
        a.volume= s.volume;
        a.loop = true;
        a.Play();
    }

    protected void cd(int i) 
    {
        CoolDown(i);
    }

    public void SetUp() 
    {
        _health = MaxHealth;
        pauseControllers = true;
    }

    protected virtual void Update()
    {
        if(_dead || pauseControllers) return;
        RunCollisionChecks();
        CalculateCollitionBehaviour();

        CalculateWalk(); // Horizontal movement
        CalculateJumpApex(); // Affects fall speed, so calculate before gravity
        CalculateGravity(); // Vertical movement
        CalculateJump(); // Possibly overrides vertical
        StunSystem();
        MoveCharacter();

        HealthSystem();

        if (RP == 2)
        {
            OnWining();
        }
    }

    #region Gather Input

    protected virtual void GatherInput()
    {
        Input = new FrameInput
        {
            JumpDown = UnityEngine.Input.GetButtonDown("Jump"),
            JumpUp = UnityEngine.Input.GetButtonUp("Jump"),
            X = UnityEngine.Input.GetAxisRaw("Horizontal")
        };
        if (Input.JumpDown)
        {
            _lastJumpPressed = Time.time;
        }
    }

    #endregion

    #region Collisions

    [Header("COLLISION")][SerializeField] private Bounds _characterBounds;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private LayerMask _roofLayer;
    [SerializeField] private LayerMask _laserLayer;
    [SerializeField] private int _detectorCount = 3;
    [SerializeField] private float _detectionRayLength = 0.1f;
    [SerializeField][Range(0.1f, 0.3f)] private float _rayBuffer = 0.1f; // Prevents side detectors hitting the ground

    private RayRange _raysUp, _raysRight, _raysDown, _raysLeft;
    private bool _colUp, _colRight, _colDown, _colLeft;
    public RaycastHit2D _hitUp, _hitDown, _hitRight, _hitLeft;

    private float _timeLeftGrounded;

    // We use these raycast checks for pre-collision information
    private void RunCollisionChecks()
    {
        // Generate ray ranges. 
        CalculateRayRanged();

        // Ground
        LandingThisFrame = false;
        (bool groundedCheck, RaycastHit2D d) = Input.FallDown? Detection(_raysDown, _groundLayer) : Detection(_raysDown, _roofLayer);
        if (_colDown && !groundedCheck) _timeLeftGrounded = Time.time; // Only trigger when first leaving
        else if (!_colDown && groundedCheck)
        {
            _coyoteUsable = true; // Only trigger when first touching
            LandingThisFrame = true;
        }

        _colDown = groundedCheck;
        _hitDown = d;
        (_colUp, _hitUp) = Detection(_raysUp, _laserLayer);
        (_colLeft, _hitLeft) = Detection(_raysLeft, _laserLayer);
        (_colRight, _hitRight) = Detection(_raysRight, _laserLayer);

        (bool, RaycastHit2D) Detection(RayRange range, LayerMask layer)
        {
            bool Collition = false;
            RaycastHit2D ray = new RaycastHit2D();

            foreach (Vector2 point in EvaluateRayPositions(range))
            {
                ray = Physics2D.Raycast(point, range.Dir, _detectionRayLength, layer);

                if (ray.collider != null)
                {
                    Collition = true;
                }

            }
            return (Collition, ray);
            //return CalculatePointsPositions(range).Any((point) => Physics2D.Raycast(point, range.Dir,out hit,_detectionRayLength, layer));
        }
    }

    protected void CalculateCollitionBehaviour()
    {
        RaycastHit2D hit = ReturnHit();
        if (hit)
        {
            Laser p = hit.collider.GetComponent<Laser>();

            if (p != null)
            {
                this.TakeDamage(-1);
            }
        }
    }

    protected RaycastHit2D ReturnHit()
    {
        if (_hitRight) return _hitRight;
        if (_hitUp) return _hitUp;
        if (_hitDown) return _hitDown;
        return _hitLeft;
    }

    private void CalculateRayRanged()
    {
        // This is crying out for some kind of refactor. 
        var b = new Bounds(transform.position, _characterBounds.size);

        _raysDown = new RayRange(b.min.x + _rayBuffer, b.min.y, b.max.x - _rayBuffer, b.min.y, Vector2.down);
        _raysUp = new RayRange(b.min.x + _rayBuffer, b.max.y, b.max.x - _rayBuffer, b.max.y, Vector2.up);
        _raysLeft = new RayRange(b.min.x, b.min.y + _rayBuffer, b.min.x, b.max.y - _rayBuffer, Vector2.left);
        _raysRight = new RayRange(b.max.x, b.min.y + _rayBuffer, b.max.x, b.max.y - _rayBuffer, Vector2.right);
    }

    private IEnumerable<Vector2> EvaluateRayPositions(RayRange range)
    {
        for (var i = 0; i < _detectorCount; i++)
        {
            var t = (float)i / (_detectorCount - 1);
            yield return Vector2.Lerp(range.Start, range.End, t);
        }
    }

    private void OnDrawGizmos()
    {
        // Bounds
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + _characterBounds.center, _characterBounds.size);

        // Rays
        if (!Application.isPlaying)
        {
            CalculateRayRanged();
            Gizmos.color = Color.blue;
            foreach (var range in new List<RayRange> { _raysUp, _raysRight, _raysDown, _raysLeft })
            {
                foreach (var point in EvaluateRayPositions(range))
                {
                    Gizmos.DrawRay(point, range.Dir * _detectionRayLength);
                }
            }
        }

        if (!Application.isPlaying) return;

        // Draw the future position. Handy for visualizing gravity
        Gizmos.color = Color.red;
        var move = new Vector3(_currentHorizontalSpeed, _currentVerticalSpeed) * Time.deltaTime;
        Gizmos.DrawWireCube(transform.position + move, _characterBounds.size);
    }

    #endregion

    #region Walk

    [Header("WALKING")][SerializeField] private float _acceleration = 90;
    [SerializeField] private float _moveClamp = 13;
    [SerializeField] private float _deAcceleration = 60f;
    [SerializeField] private float _apexBonus = 2;

    private void CalculateWalk()
    {
        if (Input.X != 0)
        {
            // Set horizontal move speed
            _currentHorizontalSpeed += Input.X * _acceleration * Time.deltaTime;


            // clamped by max frame movement
            _currentHorizontalSpeed = Mathf.Clamp(_currentHorizontalSpeed, -_moveClamp, _moveClamp);

            // Apply bonus at the apex of a jump
            var apexBonus = Mathf.Sign(Input.X) * _apexBonus * _apexPoint;
            _currentHorizontalSpeed += apexBonus * Time.deltaTime;

            footsteps.SetActive(true);
        }
        else
        {
            // No input. Let's slow the character down
            _currentHorizontalSpeed = Mathf.MoveTowards(_currentHorizontalSpeed, 0, _deAcceleration * Time.deltaTime);
            footsteps.SetActive(false);
        }

        if (_currentHorizontalSpeed > 0 && _colRight  || _currentHorizontalSpeed < 0 && _colLeft)
        {
            _currentHorizontalSpeed = 0;
            footsteps.SetActive(false);

        }
    }

    #endregion

    #region Gravity

    [Header("GRAVITY")][SerializeField] private float _fallClamp = -20f;
    [SerializeField] private float _minFallSpeed = 80f;
    [SerializeField] private float _maxFallSpeed = 120f;
    private float _fallSpeed;

    private void CalculateGravity()
    {
        if (_colDown)
        {
            // Move out of the ground
            if (_currentVerticalSpeed < 0) _currentVerticalSpeed = 0;
        }
        else
        {
            // Add downward force while ascending if we ended the jump early
            var fallSpeed = _endedJumpEarly && _currentVerticalSpeed > 0 ? _fallSpeed * _jumpEndEarlyGravityModifier : _fallSpeed;

            // Fall
            _currentVerticalSpeed -= fallSpeed * Time.deltaTime;

            // Clamp
            if (_currentVerticalSpeed < _fallClamp) _currentVerticalSpeed = _fallClamp;
        }

    }

    #endregion

    #region Jump

    [Header("JUMPING")][SerializeField] private float _jumpHeight = 30;
    [SerializeField] private float _jumpApexThreshold = 10f;
    [SerializeField] private float _coyoteTimeThreshold = 0.1f;
    [SerializeField] private float _jumpBuffer = 0.1f;
    [SerializeField] private float _jumpEndEarlyGravityModifier = 3;
    private bool _coyoteUsable;
    private bool _endedJumpEarly = true;
    private float _apexPoint; // Becomes 1 at the apex of a jump
    protected float _lastJumpPressed;
    private bool CanUseCoyote => _coyoteUsable && !_colDown && _timeLeftGrounded + _coyoteTimeThreshold > Time.time;
    private bool HasBufferedJump => _colDown && _lastJumpPressed + _jumpBuffer > Time.time;

    private void CalculateJumpApex()
    {
        if (!_colDown)
        {
            // Gets stronger the closer to the top of the jump
            _apexPoint = Mathf.InverseLerp(_jumpApexThreshold, 0, Mathf.Abs(Velocity.y));
            _fallSpeed = Mathf.Lerp(_minFallSpeed, _maxFallSpeed, _apexPoint);
        }
        else
        {
            _apexPoint = 0;
        }
    }

    private void CalculateJump()
    {
        // Jump if: grounded or within coyote threshold || sufficient jump buffer
        if (Input.JumpDown && CanUseCoyote || HasBufferedJump)
        {
            _currentVerticalSpeed = _jumpHeight;
            _endedJumpEarly = false;
            _coyoteUsable = false;
            _timeLeftGrounded = float.MinValue;
            JumpingThisFrame = true;
            AudioManager.instance.PlayAudio(0);
        }
        else
        {
            JumpingThisFrame = false;
        }

        // End the jump early if button released
        if (!_colDown && Input.JumpUp && !_endedJumpEarly && Velocity.y > 0)
        {
            // _currentVerticalSpeed = 0;
            _endedJumpEarly = true;
        }

        if (_colUp)
        {
            if (_currentVerticalSpeed > 0) _currentVerticalSpeed = 0;
        }
    }

    #endregion

    #region Move

    [Header("MOVE")]
    [SerializeField, Tooltip("Raising this value increases collision accuracy at the cost of performance.")]
    private int _freeColliderIterations = 10;

    // We cast our bounds before moving to avoid future collisions
    private void MoveCharacter()
    {
        Flip();
        var pos = transform.position;
        RawMovement = new Vector3(_currentHorizontalSpeed, _currentVerticalSpeed); // Used externally
        var move = RawMovement * Time.deltaTime;
        var furthestPoint = pos + move;
        

        // check furthest movement. If nothing hit, move and don't do extra checks
        var hit = Physics2D.OverlapBox(furthestPoint, _characterBounds.size, 0, _groundLayer);
        if (!hit)
        {
            transform.position += move;
            return;
        }

        // otherwise increment away from current pos; see what closest position we can move to
        var positionToMoveTo = transform.position;
        for (int i = 1; i < _freeColliderIterations; i++)
        {
            // increment to check all but furthestPoint - we did that already
            var t = (float)i / _freeColliderIterations;
            var posToTry = Vector2.Lerp(pos, furthestPoint, t);

            if (Physics2D.OverlapBox(posToTry, _characterBounds.size, 0, _groundLayer))
            {
                transform.position = positionToMoveTo;

                // We've landed on a corner or hit our head on a ledge. Nudge the player gently
                if (i == 1)
                {
                    if (_currentVerticalSpeed < 0) _currentVerticalSpeed = 0;
                    var dir = transform.position - hit.transform.position;
                    transform.position += dir.normalized * move.magnitude;
                }

                return;
            }

            positionToMoveTo = posToTry;
        }
    }

    #endregion

    #region HealthSystem
    [Header("HealthSystem")]
    [SerializeField] private int StartHealth;
    [SerializeField] private int MaxHealth;
    [SerializeField] private float InvensivilityTime;
    private float _invensibleTimer;
    protected bool _attacked;
    [SerializeField] public int _health;
    public int Health { get => _health;}
    private bool _dead = false;
    private bool _invensible;

    public void TakeDamage(int dagame) 
    {
        if (_attacked || _invensible) return;
        _health = Mathf.Clamp(_health + dagame, 0, MaxHealth);
        Debug.Log(_health); 
        _attacked = true;
        _invensibleTimer = InvensivilityTime;
        _invensible = true;
        OnChangeLife();
        AudioManager.instance.PlayAudio(4);
    }

    private void HealthSystem() 
    {
        if (_health == 0) 
        {
            _dead= true;
            OnLosing();
        }

        if (_attacked || _invensible) 
        {
            if (_invensibleTimer <= 0)
            {
                _attacked= false;
                _invensible= false;
            }
            else
            {
                _invensibleTimer -= Time.deltaTime;
            }
        }
    }

    public void fullHeal() 
    {
        _health = MaxHealth;
        GameUIManager.instance.SetLifeToFull();
    }

    #endregion

    #region StunSystem

    [SerializeField] private float StunTimer;
    private float _stuntimer;
    private bool _stuned = false;

    public void Stun() 
    {
        if(_stuned || _invensible) return;
        _stuntimer = StunTimer;
        _stuned = true;
    }

    private void StunSystem() 
    {
        //Debug.Log(_stuned);
        if (_stuned) 
        {
            _currentHorizontalSpeed = 0;
            _currentVerticalSpeed = 0;
            if (_stuntimer <= 0) 
            {
                _stuned= false;
            }
            else
            {
                _stuntimer-= Time.deltaTime;
            }
        }
    }

    #endregion

    #region Flip

    protected bool isFacingRight = true;

    protected void Flip()
    {
        //Flip
        if (Input.X == 1) //mira a la derecha
            transform.eulerAngles = new Vector2(0, 0);
        if (Input.X == -1)//mira a la izquierdad
            transform.eulerAngles = new Vector2(0, 180);

        //if (isFacingRight && _currentHorizontalSpeed < 0f || !isFacingRight && _currentHorizontalSpeed > 0f)
        //{
        //    isFacingRight = !isFacingRight;
        //    Vector3 localScale = transform.localScale;
        //    Vector3 launchPositionScale = launchPosition.localScale;
        //    localScale.x *= -1f;
        //    launchPositionScale.x *= -1f;
        //    transform.localScale = localScale;
        //    launchPosition.localScale = launchPositionScale;
        //}
    }

    #endregion

    #region Reliquias

    public void plusATK(int i) 
    {
        MagicPellets magicPellets = gameObject.GetComponent<MagicPellets>();
        ElementalBall elementalBall = gameObject.GetComponent<ElementalBall>();

        magicPellets.damage *= i;
        elementalBall.damage *= i;
    }

    public void plusHealth() 
    {
        MaxHealth = 8;
        _health = 8;
        OnChangeLife();
        GameUIManager.instance.SetLifeToFull();
    }

    public void lessCooldown(float i) 
    {
        Alchemist alchemist = DungeonManager.instance.Gicamu.GetComponent<Alchemist>();
        Wizard wizard = DungeonManager.instance.Gicamu.GetComponent<Wizard>();

        alchemist.barrierCoolDown -= i;
        alchemist.pelletsCoolDown -= i;
        wizard.ballCoolDown -= i;
        wizard.stunCoolDown -= i;
    }

    #endregion
}
