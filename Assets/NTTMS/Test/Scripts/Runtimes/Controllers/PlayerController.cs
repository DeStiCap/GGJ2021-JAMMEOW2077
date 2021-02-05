using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DSC.Core;
using DSC.Input;
using UnityEngine.Events;

namespace NTTMS.Test
{
    public class PlayerController : MonoBehaviour
    {
        #region Variable

        #region Variable - Inspector

        [SerializeField] PlayerCharacterType m_eCharacterType;
        [Min(0)]
        [SerializeField] protected float m_fMoveSpeed = 5.2f;
        [Min(0)]
        [SerializeField] protected float m_fCrouchSpeed = 2.6f;
        [Min(0)]
        [SerializeField] protected float m_fClimbSpeed = 5.2f;
        [Min(0)]
        [SerializeField] protected float m_fJumpForce = 5f;

        [Min(0)]
        [SerializeField] protected float m_fJumpInputHold = 3f;

        [Header("Event")]
        [SerializeField] UnityEvent m_hFlipEvent;

        [Header("Reference")]
        [SerializeField] PlayerAnimatorController m_hAnimController;
        [SerializeField] GameObject m_hStandCheckGroup;
        [SerializeField] GameObject m_hCrouchCheckGroup;

        #endregion

        #region Variable - Property

        public PlayerAnimatorController animatorController { get { return m_hAnimController; } }

        public PlayerCharacterType characterType { get { return m_eCharacterType; } }

        public bool isGrounded { get { return m_bIsGrounded; } }

        public bool isJumping { get { return m_bIsJumping; } }

        public bool isClimbing { get { return m_bClimbing; } }

        public ClimbObjectController climbableController { get { return m_hClimbController; } set { m_hClimbController = value; } }

        #endregion

        protected Rigidbody2D m_hRigid;
        protected CapsuleCollider2D m_hCol;

        protected int m_nPlayerID;

        protected bool m_bFacingRight = true;
        protected bool m_bIsGrounded;
        protected bool m_bIsCeiling;
        protected bool m_bIsWallBack;
        protected bool m_bIsWallForward;

        protected bool m_bIsJumping;
        protected float m_fJumpDelayCheck;

        protected float? m_fJumpInputStartTime;


        protected bool m_bCrouching;

        protected float m_fGravityScaleOrigin;
        protected Vector2 m_vCurrentVelocity;

        protected Vector2 m_vColSizeOrigin;
        protected Vector2 m_vColOffsetOrigin;

        protected Vector2 m_vInputAxis;
        protected RaycastHit2D[] m_arrRayHit = new RaycastHit2D[5];

        protected ClimbObjectController m_hClimbController;

        protected bool m_bClimbing;

        protected IInteractable m_hInteractable;
        protected float? m_fLastFrameGetInteractable;

        #endregion

        #region Base - Mono

        protected virtual void Awake()
        {
            m_hRigid = GetComponent<Rigidbody2D>();
            m_hCol = GetComponent<CapsuleCollider2D>();
            m_fGravityScaleOrigin = m_hRigid.gravityScale;
            m_hRigid.gravityScale = 0;

            m_vColSizeOrigin = m_hCol.size;
            m_vColOffsetOrigin = m_hCol.offset;

            if (m_eCharacterType == PlayerCharacterType.Uncle)
            {
                m_nPlayerID = 1;
            }
        }

        protected virtual void OnEnable()
        {
            Global_InputManager.AddInputEventListener(m_nPlayerID, (int)InputButtonType.South, ActionButtonDown, ActionButtonHold, ActionButtonUp);
        }

        protected virtual void OnDisable()
        {
            Global_InputManager.RemoveInputEventListener(m_nPlayerID, (int)InputButtonType.South, ActionButtonDown, ActionButtonHold, ActionButtonUp);
        }

        protected virtual void Update()
        {
            m_vInputAxis = Global_InputManager.GetRawAxis(m_nPlayerID, 0);

            JumpDelayCheckCounting();
            CheckClimbInput();
        }

        protected virtual void FixedUpdate()
        {
            AddGravityVelocity();
            CheckResetVelocityY();
            CheckJumpEnd();
            Crouch();
            Move();
            CheckFlipCharacter();
            UpdateAnimation();
            ClearLastInteractable();
        }

        #endregion

        #region Events

        #region Events - Input

        protected virtual void ActionButtonDown()
        {
            JumpInputStart();
            //Jump();
        }

        protected virtual void ActionButtonHold()
        {
            JumpInputUpdate();
        }

        protected virtual void ActionButtonUp()
        {
            JumpInputEnd();
        }

        #endregion

        public void SetIsGrounded(bool bIsGrounded)
        {
            m_bIsGrounded = bIsGrounded;

            // Temp
            if (IsUncle())
                bIsGrounded = true;

            m_hAnimController.SetGrounded(bIsGrounded);

            if (bIsGrounded && m_bIsJumping && m_fJumpDelayCheck <= 0)
            {
                IsJump(false);
            }
        }

        public void SetIsCeiling(bool bIsCeiling)
        {
            m_bIsCeiling = bIsCeiling;
        }

        public void SetIsWallForward(bool bIsWall)
        {
            m_bIsWallForward = bIsWall;
        }

        public void SetIsWallBack(bool bIsWall)
        {
            m_bIsWallBack = bIsWall;
        }

        public void GetInteractableObject(GameObject hGO)
        {
            var hInteractable = hGO.GetComponent<IInteractable>();
            if (hInteractable != null)
            {
                if (!hInteractable.CanInteraction(this))
                    return;

                if (m_hInteractable != null && m_hInteractable != hInteractable)
                    m_hInteractable.ShowInteractionUI(this, false);

                m_hInteractable = hInteractable;
                hInteractable.ShowInteractionUI(this, true);

                m_fLastFrameGetInteractable = Time.time;
            }
        }

        #endregion

        #region Base - Main

        protected virtual void Move()
        {
            if (m_bIsGrounded && !m_bClimbing && m_fJumpInputStartTime.HasValue)
                return;

            float fDeltaTime = Time.deltaTime;
            Vector2 vMovePosition = Vector2.zero;
            float m_fMoveHorizontal = m_vInputAxis.x;
            float m_fMoveVertical = m_vInputAxis.y;

            if (m_bClimbing)
                m_fMoveHorizontal = 0;
            else
                m_fMoveVertical = 0;

            // Add velocity value to move.
            vMovePosition += m_vCurrentVelocity * fDeltaTime;

            float fMoveSpeed = m_bCrouching ? m_fCrouchSpeed : m_fMoveSpeed;
            vMovePosition.x += m_fMoveHorizontal * fMoveSpeed * fDeltaTime;
            vMovePosition.y += m_fMoveVertical * m_fClimbSpeed * fDeltaTime;
            ChangeMoveXIfHasObstacle(ref vMovePosition);

            vMovePosition += m_hRigid.position;
            m_hRigid.MovePosition(vMovePosition);
        }

        protected virtual void JumpInputStart()
        {
            if (!m_bIsGrounded || m_bClimbing || m_bCrouching)
                return;

            m_fJumpInputStartTime = Time.time;
        }

        protected virtual void JumpInputEnd()
        {
            m_fJumpInputStartTime = null;
        }

        protected virtual void JumpInputUpdate()
        {
            if (!m_fJumpInputStartTime.HasValue)
                return;

            if (Time.time >= m_fJumpInputStartTime + m_fJumpInputHold)
            {
                Jump();
                JumpInputEnd();
            }
        }

        protected virtual void Jump()
        {
            if (IsUncle())
                return;

            if (!m_bIsGrounded || m_bIsCeiling)
                return;

            m_vCurrentVelocity.y += m_fJumpForce;

            IsJump(true);
            m_fJumpDelayCheck = 0.2f;
        }

        protected virtual void JumpDelayCheckCounting()
        {
            if (m_fJumpDelayCheck <= 0)
                return;

            m_fJumpDelayCheck -= Time.deltaTime;
            if (m_fJumpDelayCheck < 0)
                m_fJumpDelayCheck = 0;
        }

        protected virtual void Crouch()
        {
            if (IsUncle())
                return;

            bool bCrouching = m_bIsGrounded && m_vInputAxis.y < 0;
            if (m_bClimbing || m_fJumpInputStartTime.HasValue)
                bCrouching = false;

            if ((m_bCrouching == bCrouching) || (m_bCrouching && m_bIsCeiling))
                return;

            m_bCrouching = bCrouching;
            m_hAnimController.SetCrouch(m_bCrouching);

            if (m_bCrouching)
            {
                Vector2 vSize = m_vColSizeOrigin;
                vSize.y *= 0.6f;
                m_hCol.size = vSize;

                Vector2 vOffset = m_vColOffsetOrigin;
                vOffset.y += (vSize.y - m_vColSizeOrigin.y) * 0.5f;
                m_hCol.offset = vOffset;

                m_hStandCheckGroup.SetActive(false);
                m_hCrouchCheckGroup.SetActive(true);
            }
            else
            {
                m_hCol.size = m_vColSizeOrigin;
                m_hCol.offset = m_vColOffsetOrigin;

                m_hStandCheckGroup.SetActive(true);
                m_hCrouchCheckGroup.SetActive(false);
            }
        }

        protected virtual void CheckClimbInput()
        {
            if (climbableController == null)
                return;

            Vector2 vClimbCenter = climbableController.GetCenterPosition();
            float xDistance = Mathf.Abs(vClimbCenter.x - transform.position.x);
            if (xDistance > 0.2f)
                return;

            bool bClimbing = m_bClimbing;
            bool bBelowCenter = climbableController.IsBelowCenter(transform.position);
            float fPlayerBottomPosition = GetBottomPosition().y;
            float fTopPosition = climbableController.GetTopPosition().y;
            bool bOverTop = fPlayerBottomPosition >= fTopPosition - 0.1f;

            if ((m_vInputAxis.y > 0 && !bOverTop)
                || (m_vInputAxis.y < 0 && !bBelowCenter && bOverTop))
            {
                bClimbing = true;
            }
            else if ((m_vInputAxis.y < 0 && bBelowCenter && m_bIsGrounded)
                || (!bBelowCenter && fPlayerBottomPosition >= fTopPosition - 0.1f))
            {
                bClimbing = false;
            }

            if (bClimbing != m_bClimbing)
            {
                if (bClimbing)
                    ClimbStart();
                else
                    ClimbEnd();
            }
        }

        protected virtual void ClimbStart()
        {
            m_bClimbing = true;
            m_hAnimController.SetClimb(true);

            Vector2 vPosition = transform.position;
            vPosition.x = climbableController.GetCenterPosition().x;
            transform.position = vPosition;

            gameObject.layer = LayerMask.NameToLayer(LayerName.climb);
        }

        protected virtual void ClimbEnd()
        {
            m_bClimbing = false;
            m_hAnimController.SetClimb(false);

            gameObject.layer = LayerMask.NameToLayer(LayerName.player);
        }

        protected void UpdateAnimation()
        {
            bool bWalk = m_vInputAxis.x != 0;
            if (m_fJumpInputStartTime.HasValue)
                bWalk = false;
                
            m_hAnimController.SetWalk(bWalk);
        }

        #endregion

        #region Helper

        protected void AddGravityVelocity()
        {
            if (m_bIsGrounded || m_bClimbing)
                return;

            m_vCurrentVelocity += Physics2D.gravity * m_fGravityScaleOrigin * Time.deltaTime;
        }

        protected void CheckResetVelocityY()
        {
            if ((m_bIsGrounded && m_vCurrentVelocity.y < 0)
                || (m_bIsCeiling && m_vCurrentVelocity.y > 0)
                || m_bClimbing)
                m_vCurrentVelocity.y = 0;
        }

        protected void ChangeMoveXIfHasObstacle(ref Vector2 vMove)
        {
            if (vMove.x == 0)
                return;

            bool bMovingForward = (vMove.x > 0) == m_bFacingRight;
            if ((bMovingForward && m_bIsWallForward)
                || (!bMovingForward && m_bIsWallBack))
            {
                vMove.x = 0;
            }
        }

        protected void CheckFlipCharacter()
        {
            if ((m_vInputAxis.x > 0 && !m_bFacingRight)
                || m_vInputAxis.x < 0 && m_bFacingRight)
                FlipCharacter();
        }

        protected void FlipCharacter()
        {
            m_bFacingRight = !m_bFacingRight;
            transform.Rotate(new Vector3(0, 180, 0));
            m_hFlipEvent?.Invoke();
        }

        protected void CheckJumpEnd()
        {
            if (!m_bIsJumping)
                return;

            if (m_vCurrentVelocity.y <= 0)
            {
                IsJump(false);
            }
        }

        protected void IsJump(bool bJump)
        {
            m_bIsJumping = bJump;
            m_hAnimController.SetJump(bJump);
        }

        protected Vector2 GetBottomPosition()
        {
            Vector2 vPosition = m_hRigid.position + m_hCol.offset;
            vPosition.y -= m_hCol.size.y * 0.5f;
            return vPosition;
        }

        protected bool IsUncle()
        {
            return m_eCharacterType == PlayerCharacterType.Uncle;
        }

        protected void ClearLastInteractable()
        {
            if (m_hInteractable == null || !m_fLastFrameGetInteractable.HasValue)
                return;

            if (Time.time <= m_fLastFrameGetInteractable + 0.04f)
                return;

            m_fLastFrameGetInteractable = null;
            m_hInteractable.ShowInteractionUI(this, false);
            m_hInteractable = null;
        }

        #endregion
    }
}