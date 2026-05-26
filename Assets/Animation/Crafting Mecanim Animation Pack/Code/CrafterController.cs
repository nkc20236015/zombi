#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace CraftingAnims
{
	public class CrafterController:MonoBehaviour
	{
		// Components.
		[HideInInspector] public Animator animator;
		[HideInInspector] public CrafterShowItem showItem;
		[HideInInspector] public CrafterIKHands crafterIKhands;
		[HideInInspector] public GUIControls guiControls;
		[HideInInspector] public Rigidbody rb;

		// Variables.
		public CrafterState charState;
		public float animationSpeed = 1;

		// Objects.
		public GameObject hatchet;
		public GameObject hammer;
		public GameObject fishingpole;
		public GameObject shovel;
		public GameObject box;
		public GameObject food;
		public GameObject drink;
		public GameObject saw;
		public GameObject pickaxe;
		public GameObject sickle;
		public GameObject rake;
		public GameObject chair;
		public GameObject ladder;
		public GameObject lumber;
		public GameObject pushpull;
		public GameObject sphere;
		public GameObject cart;
		public GameObject paintbrush;
		public GameObject spear;

		// Actions.
		[HideInInspector] public bool isMoving;
		[HideInInspector] public bool isLocked;
		[HideInInspector] public bool isGrounded;
		[HideInInspector] public bool isSpearfishing;
		private Coroutine coroutineLock = null;
		private Vector3 newVelocity;
		private bool isFacing = false;
		private bool isRunning = false;
		private float pushpullTime = 0f;
		private bool carryItem = false;

		// Input.
		private bool allowedInput = true;
		private Vector3 inputVec;
		private float inputHorizontal = 0f;
		private float inputVertical = 0f;
		private float inputHorizontal2 = 0f;
		private float inputVertical2 = 0f;
		private bool inputFacing;
		private bool inputRun;

		[Header("Movement")]
		public float rotationSpeed = 10f;
		public float runSpeed = 8f;
		public float walkSpeed = 4f;
		public float spearfishingSpeed = 1.25f;
		public float crawlSpeed = 1f;

		[Header("Navigation")]
		public bool useNavMeshNavigation = false;
		[HideInInspector] public CrafterNavigation crafterNavigation;
		[HideInInspector] public bool navMeshNavigation = false;
		[HideInInspector] public bool navMeshRun = false;

		private void Awake()
		{
			// Setup animator.
			animator = GetComponentInChildren<Animator>();

			if (animator) {
				animator.gameObject.AddComponent<CrafterAnimatorController>();
				animator.GetComponent<CrafterAnimatorController>().crafterController = this;
				animator.updateMode = AnimatorUpdateMode.Normal;
				animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
				animator.SetLayerWeight(1, 1f);
				animator.SetLayerWeight(2, 0f);
			}

			rb = GetComponent<Rigidbody>();
			guiControls = GetComponent<GUIControls>();
			showItem = GetComponent<CrafterShowItem>();
			crafterIKhands = GetComponentInChildren<CrafterIKHands>();

			// If using Naviation setup CrafterNavigation, otherwise remove Navigation components.
			if (useNavMeshNavigation) { crafterNavigation = GetComponent<CrafterNavigation>(); }
			else {
				Destroy(GetComponent<CrafterNavigation>());
				Destroy(GetComponent<NavMeshAgent>());
				Destroy(GameObject.Find("Nav"));
			}
		}

		private void Start()
		{
			showItem.ItemShow("none", 0);
			charState = CrafterState.Idle;
		}

		/// <summary>
		/// Input abstraction for easier asset updates using outside control schemes.
		/// </summary>
		private void Inputs()
		{
			if (InputHelper.GetKey(KeyCode.LeftShift)) {
				inputRun = true;
			}
			else {
				inputRun = false;
			}

			// New Input System.
			#if ENABLE_INPUT_SYSTEM
			inputHorizontal = 0f;
			inputVertical = 0f;
			inputHorizontal2 = 0f;
			inputVertical2 = 0f;

			// Get gamepad reference
			var gamepad = Gamepad.current;

			// Horizontal input: A/D, Left/Right Arrows or Left Stick X.
			if (InputHelper.GetKey(KeyCode.A) || InputHelper.GetKey(KeyCode.LeftArrow)) {
				inputHorizontal = -1f;
			}
			else if (InputHelper.GetKey(KeyCode.D) || InputHelper.GetKey(KeyCode.RightArrow)) {
				inputHorizontal = 1f;
			}
			else if (gamepad != null)
				inputHorizontal = Mathf.Abs(gamepad.leftStick.x.ReadValue()) > 0.1f ? gamepad.leftStick.x.ReadValue() : 0f;

			// Vertical input: W/S, Up/Down Arrows or Left Stick Y. (inverted for typical top-down behavior)
			if (InputHelper.GetKey(KeyCode.W) || InputHelper.GetKey(KeyCode.UpArrow)) {
				inputVertical = -1f;
			}
			else if (InputHelper.GetKey(KeyCode.S) || InputHelper.GetKey(KeyCode.DownArrow)) {
				inputVertical = 1f;
			}
			else if (gamepad != null) {
				inputVertical = Mathf.Abs(gamepad.leftStick.y.ReadValue()) > 0.1f ? -gamepad.leftStick.y.ReadValue() : 0f;
			}
			// Right mouse or gamepad right trigger/shoulder to trigger facing.
			inputFacing = Mouse.current.rightButton.isPressed ||
				(gamepad != null && gamepad.rightStick.ReadValue().magnitude > 0.1f);

			// Old Input Manager.
			#else
			try {
				inputHorizontal = Input.GetAxisRaw("Horizontal");
				inputVertical = -Input.GetAxisRaw("Vertical");
				inputHorizontal2 = Input.GetAxisRaw("Horizontal2");
				inputVertical2 = -Input.GetAxisRaw("Vertical2");
				inputFacing = InputHelper.GetKey(KeyCode.Mouse1)
					|| Mathf.Abs(inputHorizontal2) > 0.1f
					|| Mathf.Abs(inputVertical2) > 0.1f;
			}
			catch (System.Exception) {
				Debug.LogWarning("Inputs not found! Please see Readme file.");
			}

			#endif
		}

		#region Updates

		private void Update()
		{
			// Check if input is allowed.  Disable it if using NavMesh.
			if (allowedInput && !navMeshNavigation) { Inputs(); }

			// Facing switch.
			if (inputFacing) { isFacing = true; }
			else { isFacing = false; }

			// Slow time.
			if (InputHelper.GetKeyDown(KeyCode.T)) {
				if (Time.timeScale != 1) { Time.timeScale = 1; }
				else { Time.timeScale = 0.15f; }
			}

			// Pause.
			if (InputHelper.GetKeyDown(KeyCode.P)) {
				if (Time.timeScale != 1) { Time.timeScale = 1; }
				else { Time.timeScale = 0f; }
			}

			// Push-Pull
			if (charState != CrafterState.PushPull) { CameraRelativeInput(); }
			else { PushPull(); }
		}

		private void FixedUpdate()
		{
			CheckForGrounded();

			// If locked, apply Root motion.
			if (!isLocked) {
				if (charState == CrafterState.Climb
					|| charState == CrafterState.PushPull
					|| charState == CrafterState.Laydown
					|| charState == CrafterState.Use) {
					animator.applyRootMotion = true;
					isMoving = false;
					rb.useGravity = false;
				}
				else {
					animator.applyRootMotion = false;
					rb.useGravity = true;
				}
			}

			// Change animator Animation Speed.
			animator.SetFloat("AnimationSpeed", animationSpeed);
		}

		private void LateUpdate()
		{
			// Running.
			if (inputRun) {

				// Don't run with Box, Cart, Lumber, etc.
				if (charState != CrafterState.Box
					&& charState != CrafterState.Cart
					&& charState != CrafterState.Overhead
					&& charState != CrafterState.PushPull
					&& charState != CrafterState.Lumber
					&& charState != CrafterState.Use) {
					isRunning = true;
					isFacing = false;
				}
			}
			else { isRunning = false; }

			// If not using Navmesh, update Character movement.
			if (!navMeshNavigation) {
				if (UpdateMovement() > 0) {
					isMoving = true;
					animator.SetBool("Moving", true);
				}
				else {
					isMoving = false;
					animator.SetBool("Moving", false);
				}

				// Get local velocity of charcter and update animator with values.
				float velocityXel = transform.InverseTransformDirection(rb.linearVelocity).x;
				float velocityZel = transform.InverseTransformDirection(rb.linearVelocity).z;

				// Set animator values if not pushpull.
				if (charState != CrafterState.PushPull) {
					animator.SetFloat("Velocity X", velocityXel / runSpeed);
					animator.SetFloat("Velocity Y", velocityZel / runSpeed);
				}
			}
		}

		/// <summary>
		/// Moves the character.
		/// </summary>
		private float UpdateMovement()
		{
			Vector3 motion = inputVec;

			// Reduce input for diagonal movement.
			if (motion.magnitude > 1) { motion.Normalize(); }

			if (!isLocked
				&& charState != CrafterState.PushPull
				&& charState != CrafterState.Laydown
				&& charState != CrafterState.Crawl) {

				// Set speed by walking / running.
				if (isRunning) { newVelocity = motion * runSpeed; }
				else if (isSpearfishing) { newVelocity = motion * spearfishingSpeed; }
				else { newVelocity = motion * walkSpeed; }
			}
			else if (charState == CrafterState.Crawl) { newVelocity = motion * crawlSpeed; }

			// Aiming or rotate towards movement direction.
			if (isFacing
				&& charState != CrafterState.Box
				&& charState != CrafterState.Lumber
				&& charState != CrafterState.Overhead) {
				Facing();
			}
			else {
				if (!isLocked && charState != CrafterState.PushPull
					&& charState != CrafterState.Laydown
					&& charState != CrafterState.Use) {
					RotateTowardsMovementDir();
				}
			}

			// If character is falling use momentum.
			newVelocity.y = rb.linearVelocity.y;
			rb.linearVelocity = newVelocity;

			// Return a movement value for the animator.
			return inputVec.magnitude;
		}

		#endregion

		/// <summary>
		/// Checks if character is within a certain distance from the ground, and markes it IsGrounded.
		/// </summary>
		private void CheckForGrounded()
		{
			float distanceToGround;
			float threshold = .45f;
			Vector3 offset = new Vector3(0, 0.4f, 0);
			if (Physics.Raycast((transform.position + offset), -Vector3.up, out RaycastHit hit, 100f)) {
				distanceToGround = hit.distance;
				if (distanceToGround < threshold) { isGrounded = true; }
				else { isGrounded = false; }
			}
		}

		/// <summary>
		/// All movement is based off camera facing.
		/// </summary>
		private void CameraRelativeInput()
		{
			// Camera relative movement.
			Transform cameraTransform = Camera.main.transform;

			// Forward vector relative to the camera along the x-z plane.
			Vector3 forward = cameraTransform.TransformDirection(Vector3.forward);
			forward.y = 0;
			forward = forward.normalized;

			// Right vector relative to the camera always orthogonal to the forward vector.
			Vector3 right = new Vector3(forward.z, 0, -forward.x);

			// Directional inputs.
			inputVec = inputHorizontal * right + -inputVertical * forward;
		}

		/// <summary>
		/// Used when the Crafter is in Push/Pull mode.
		/// </summary>
		private void PushPull()
		{
			if (inputHorizontal == 0 && inputVertical == 0) { pushpullTime = 0; }
			if (inputHorizontal != 0) { inputVertical = 0; }
			if (inputVertical != 0) { inputHorizontal = 0; }
			pushpullTime += 0.5f * Time.deltaTime;
			float h = Mathf.Lerp(0, inputHorizontal, pushpullTime);
			float v = Mathf.Lerp(0, inputVertical, pushpullTime);
			animator.SetFloat("Velocity X", h);
			animator.SetFloat("Velocity Y", v);
		}

		/// <summary>
		/// Faces Crafter along input direction.
		/// </summary>
		private void RotateTowardsMovementDir()
		{
			if (inputVec != Vector3.zero) {
				transform.rotation = Quaternion.Slerp(transform.rotation,
					Quaternion.LookRotation(inputVec),
					Time.deltaTime * rotationSpeed);
			}
		}

		/// <summary>
		/// For facing the Crafter in a different direction than the Crafter input direction.
		/// </summary>
		private void Facing()
		{
			bool usedJoystick = false;

			// New Input System.
			#if ENABLE_INPUT_SYSTEM
			foreach (var gamepad in Gamepad.all) {
				Vector2 rightStick = gamepad.rightStick.ReadValue();

				if (rightStick.magnitude > 0.1f) {
					Vector3 joyDirection = new Vector3(-rightStick.y, 0, rightStick.x);
					Quaternion joyRotation = Quaternion.LookRotation(joyDirection.normalized);
					transform.rotation = joyRotation;
					usedJoystick = true;
					break;
				}
			}

			// Old Input Manager.
			#else
			if (Input.GetJoystickNames().Length > 0)
			{
				float inputHorizontal2 = Input.GetAxisRaw("Horizontal2");
				float inputVertical2 = Input.GetAxisRaw("Vertical2");

				if (Mathf.Abs(inputHorizontal2) > 0.1f || Mathf.Abs(inputVertical2) > 0.1f)
				{
					Vector3 joyDirection = new Vector3(inputVertical2, 0, inputHorizontal2);
					Quaternion joyRotation = Quaternion.LookRotation(joyDirection.normalized);
					transform.rotation = joyRotation;
					usedJoystick = true;
				}
			}
			#endif

			// Fallback to mouse aim.
			if (!usedJoystick) {
				Vector3 mouseWorld = Vector3.zero;

			#if ENABLE_INPUT_SYSTEM
				if (Mouse.current != null && Camera.main != null) {
					Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
					Plane plane = new Plane(Vector3.up, transform.position);
					if (plane.Raycast(ray, out float hitDist)) {
						mouseWorld = ray.GetPoint(hitDist);
					}
				}

				#else
				if (Camera.main != null) {
					Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
					Plane plane = new Plane(Vector3.up, transform.position);
					if (plane.Raycast(ray, out float hitDist)) {
						mouseWorld = ray.GetPoint(hitDist);
					}
				}
				#endif

				mouseWorld.y = transform.position.y;
				Vector3 relativePos = transform.position - mouseWorld;
				Quaternion rotation = Quaternion.LookRotation(-relativePos);
				transform.rotation = rotation;
			}
		}

		/// <summary>
		/// Prevents the Crafter from moving to apply Root Motion and let the animation drive the character position.
		/// </summary>
		/// <param name="locktime"></param>
		public void LockMovement(float locktime)
		{
			if (coroutineLock != null) { StopCoroutine(coroutineLock); }
			coroutineLock = StartCoroutine(_LockMovement(locktime));
		}

		private IEnumerator _LockMovement(float locktime)
		{
			allowedInput = false;
			isLocked = true;
			animator.applyRootMotion = true;
			if (locktime != -1f) {
				yield return new WaitForSeconds(locktime);
				isLocked = false;
				animator.applyRootMotion = false;
				allowedInput = true;
			}
		}

		/// <summary>
		/// Sets the CrafterState with a possible wait duration.
		/// </summary>
		/// <param name="waitTime"></param>
		/// <param name="state"></param>
		public void ChangeCharacterState(float waitTime, CrafterState state)
		{ StartCoroutine(_ChangeCharacterState(waitTime, state)); }

		private IEnumerator _ChangeCharacterState(float waitTime, CrafterState state)
		{
			yield return new WaitForSeconds(waitTime);
			charState = state;
		}

		/// <summary>
		/// Set Animator Trigger using legacy Animation Trigger names.
		/// </summary>
		public void TriggerAnimation(string trigger)
		{
			Debug.Log("TriggerAnimation: " + ( CrafterAnimatorTriggers )System.Enum.Parse(typeof(CrafterAnimatorTriggers), trigger) + " - " +
				( int )( CrafterAnimatorTriggers )System.Enum.Parse(typeof(CrafterAnimatorTriggers), trigger));
			animator.SetInteger("Action", ( int )( CrafterAnimatorTriggers )System.Enum.Parse(typeof(CrafterAnimatorTriggers), trigger));
			animator.SetTrigger("Trigger");
		}

		#region AnimationLayerBlending - IK

		/// <summary>
		/// Uses Avatar mask to Blend a Right Hand fist for holding items.
		/// </summary>
		/// <param name="use"></param>
		public void RightHandBlend(bool use)
		{ StartCoroutine(_RightHandBlend(use)); }

		private IEnumerator _RightHandBlend(bool use)
		{
			if (use) {
				float counter = 0f;
				while (counter < 1) {
					counter += 0.05f;
					yield return new WaitForEndOfFrame();
					animator.SetLayerWeight(3, counter);
				}
				animator.SetLayerWeight(3, 1);
			}
			else {
				float counter = 1f;
				while (counter > 0) {
					counter -= 0.05f;
					yield return new WaitForEndOfFrame();
					animator.SetLayerWeight(3, counter);
				}
				animator.SetLayerWeight(3, 0);
			}
		}

		private IEnumerator _RightHandBlendOff(float time)
		{
			yield return new WaitForSeconds(time);
			StartCoroutine(_RightHandBlend(false));
		}

		private IEnumerator _RightArmBlendOff(float time)
		{
			if (carryItem) {
				yield return new WaitForSeconds(time);
				StartCoroutine(_RightArmBlend(false));
			}
		}

		/// <summary>
		/// Uses Avatar mask to Blend a Right arm for carrying items like a torch.
		/// </summary>
		/// <param name="use"></param>
		public void RightArmBlend(bool use)
		{ StartCoroutine(_RightArmBlend(use)); }

		private IEnumerator _RightArmBlend(bool use)
		{
			if (use) {
				float counter = 0f;
				while (counter < 1) {
					counter += 0.05f;
					yield return new WaitForEndOfFrame();
					animator.SetLayerWeight(2, counter);
				}
				animator.SetLayerWeight(2, 1);
				carryItem = true;
			}
			else {
				float counter = 1f;
				while (counter > 0) {
					counter -= 0.05f;
					yield return new WaitForEndOfFrame();
					animator.SetLayerWeight(2, counter);
				}
				animator.SetLayerWeight(2, 0);
				carryItem = false;
			}
		}

		/// <summary>
		/// Blend Right Arm Carry animation on/off.
		/// </summary>
		/// <param name="carry"></param>
		public void CarryItem(bool carry)
		{
			if (carry) {
				carryItem = true;
				RightArmBlend(true);
			}
			else {
				carryItem = false;
				RightArmBlend(false);
			}
		}

		/// <summary>
		/// Blends all Hand/Arm animations overrides off.
		/// </summary>
		/// <param name="time"></param>
		public void BlendOff(float time)
		{
			guiControls.ResetCarry();
			StartCoroutine(_RightArmBlendOff(time));
			StartCoroutine(_RightHandBlendOff(time));
		}

		/// <summary>
		/// Use CrafterIKHands.cs to blend in the use of IK to position the left hand.
		/// </summary>
		public void IKBlendOn()
		{
			if (crafterIKhands != null) { crafterIKhands.BlendIK(true, 0.75f, 0.5f); }
		}

		/// <summary>
		/// Use CrafterIKHands.cs to blend out the use of IK to release the left hand.
		/// </summary>
		public void IKBlendOff()
		{
			if (crafterIKhands != null) { crafterIKhands.BlendIK(false, 0, 0.25f); }
		}

		#endregion
	}
}