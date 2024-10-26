using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FormationManager : MonoBehaviour
{
    // Character and obstacle prefabs
    public GameObject characterPrefab; // Prefab for the characters in formation
    public GameObject obstaclePrefab; // Prefab for obstacles placed by right-clicking

    // Movement parameters
    public float maxSpeed = 5f; // Maximum speed of character movement
    public float timeToTarget = 1f; // Time taken to reach the target
    public float satisfactionRadius = 0.5f; // Radius within which the character stops moving
    public float rotationSpeed = 2f; // Speed of rotation to final orientation

    private GameObject[] characters; // Array to store formation characters
    private Vector3 targetPosition; // Position on the plane to which formation will move

    // Hardcoded formation offsets and directions for finger-four pattern
    private readonly Vector3[] formationOffsets = {
        new Vector3(-3f, 0, -1.5f), // Character 1: To the left
        new Vector3(0, 0, 0),       // Character 2: Leader position, slightly in front
        new Vector3(3f, 0, -1.5f),  // Character 3: To the right, at same level as Character 1
        new Vector3(4f, 0, -3.5f)   // Character 4: Lower right of Character 3
    };

    private readonly Vector3[] formationDirections = {
        new Vector3(-1, 0, 0),  // Character 1: Facing left
        new Vector3(0, 0, 1),   // Character 2: Facing forward
        new Vector3(1, 0, 1),   // Character 3: Facing diagonal right
        new Vector3(0, 0, -1)   // Character 4: Facing down
    };

    void Start()
    {
        // Initialize the formation with four characters in the specified offsets and directions
        characters = new GameObject[4];
        for (int i = 0; i < characters.Length; i++)
        {
            characters[i] = Instantiate(characterPrefab, transform.position + formationOffsets[i], Quaternion.identity);
            characters[i].AddComponent<CharacterMovement>().Initialize(this, formationOffsets[i], formationDirections[i]);
        }
    }

    void Update()
    {
        // Left-click to set a new target position for the formation
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                targetPosition = hit.point;
                for (int i = 0; i < characters.Length; i++)
                {
                    Vector3 individualTarget = targetPosition + formationOffsets[i];
                    characters[i].GetComponent<CharacterMovement>().SetTarget(individualTarget);
                }
            }
        }

        // Right-click to place an obstacle at clicked position
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Instantiate(obstaclePrefab, hit.point, Quaternion.identity);
            }
        }
    }

    // Nested class to handle individual character movement
    public class CharacterMovement : MonoBehaviour
    {
        private FormationManager manager; // Reference to the formation manager
        private Rigidbody rb; // Rigidbody component for physics-based movement
        private Vector3 targetPosition; // Target position for this character
        private Vector3 fixedDirection; // Fixed final orientation for this character
        private bool hasReachedTarget = false; // Flag to check if target is reached

        // Initialization method to set up formation offset and direction
        public void Initialize(FormationManager manager, Vector3 offset, Vector3 direction)
        {
            this.manager = manager;
            this.fixedDirection = direction;
            rb = GetComponent<Rigidbody>();

            // Freeze unnecessary rotation axes to keep character stable on the ground
            rb.constraints = RigidbodyConstraints.FreezeRotationX |
                             RigidbodyConstraints.FreezeRotationZ |
                             RigidbodyConstraints.FreezePositionY;

            // Set initial orientation to face the assigned direction
            transform.rotation = Quaternion.LookRotation(fixedDirection);
        }

        // Sets the target position for the character based on the formation target
        public void SetTarget(Vector3 target)
        {
            targetPosition = target;
            hasReachedTarget = false; // Reset the reached target flag when a new target is set
        }

        void FixedUpdate()
        {
            // If character has reached its target, maintain final orientation
            if (hasReachedTarget)
            {
                rb.velocity = Vector3.zero;
                // Smoothly rotate to final orientation if stationary
                Quaternion targetRotation = Quaternion.LookRotation(fixedDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * manager.rotationSpeed);
                return;
            }

            // Calculate direction to the target while maintaining movement on the ground plane
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0;

            // Stop movement when within the satisfaction radius
            if (direction.magnitude < manager.satisfactionRadius)
            {
                rb.velocity = Vector3.zero;
                hasReachedTarget = true;
                return;
            }

            // Scale velocity to reach the target in the desired time
            Vector3 velocity = direction / manager.timeToTarget;
            if (velocity.magnitude > manager.maxSpeed)
            {
                velocity.Normalize();
                velocity *= manager.maxSpeed;
            }

            // Obstacle avoidance without rotation modification
            Collider[] obstacles = Physics.OverlapSphere(transform.position, 1f);
            bool isAvoidingObstacle = false; // Flag to check if obstacle avoidance is active
            foreach (var obstacle in obstacles)
            {
                if (obstacle.CompareTag("Obstacle"))
                {
                    Vector3 avoidDirection = Vector3.Cross(direction, Vector3.up).normalized;
                    velocity += avoidDirection * manager.maxSpeed * 0.5f;
                    isAvoidingObstacle = true; // Set flag if avoiding obstacle
                }
            }

            // Apply calculated velocity to the Rigidbody
            rb.velocity = velocity;

            // Rotate only if not avoiding obstacle
            if (!isAvoidingObstacle && velocity.sqrMagnitude > 0.01f)
            {
                Vector3 flatDirection = new Vector3(velocity.x, 0, velocity.z); // Flatten direction for smooth rotation
                Quaternion targetRotation = Quaternion.LookRotation(flatDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
            }
        }
    }
}
