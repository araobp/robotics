using UnityEngine;
using UnityEngine.UI;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;

public class RobotTest : MonoBehaviour
{

    [SerializeField] GameObject work;

    [SerializeField] private float rotationSwingSpeed = 100f;
    [SerializeField] GameObject swingAxis;

    [SerializeField] private float rotationBoomSpeed = 100f;
    [SerializeField] GameObject boomAxis;

    [SerializeField] private float rotationArmSpeed = 100f;
    [SerializeField] GameObject armAxis;

    [SerializeField] private float rotationHandSpeed = 100f;
    [SerializeField] GameObject handAxis;

    [SerializeField] Toggle toggleLookDown;

    [Header("IK Settings")]
    [SerializeField] private float ikRotationSpeed = 5f;

    Quaternion initialSwingRotation;
    Quaternion initialBoomRotation;
    Quaternion initialArmRotation;
    Quaternion initialHandRotation;

    // IK rotation targets
    private Quaternion targetSwingRotation;
    private Quaternion targetBoomRotation;
    private Quaternion targetArmRotation;
    private Quaternion targetHandRotation;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        initialSwingRotation = swingAxis.transform.localRotation;
        initialBoomRotation = boomAxis.transform.localRotation;
        initialArmRotation = armAxis.transform.localRotation;
        initialHandRotation = handAxis.transform.localRotation;

        // Initialize targets to the initial rotation
        targetSwingRotation = initialSwingRotation;
        targetBoomRotation = initialBoomRotation;
        targetArmRotation = initialArmRotation;
        targetHandRotation = initialHandRotation;

        Invoke("IKTest", 1f);
    }


    // Update is called once per frame
    void Update()
    {
        swingAxis.transform.localRotation = Quaternion.Slerp(swingAxis.transform.localRotation, targetSwingRotation, Time.deltaTime * ikRotationSpeed);
        boomAxis.transform.localRotation = Quaternion.Slerp(boomAxis.transform.localRotation, targetBoomRotation, Time.deltaTime * ikRotationSpeed);
        armAxis.transform.localRotation = Quaternion.Slerp(armAxis.transform.localRotation, targetArmRotation, Time.deltaTime * ikRotationSpeed);
        handAxis.transform.localRotation = Quaternion.Slerp(handAxis.transform.localRotation, targetHandRotation, Time.deltaTime * ikRotationSpeed);
    }

    /* This function is my original implementation of Inverse Kinematics for the robot arm.
     * It calculates the necessary joint angles to position the end effector (hand)
     * at the target position defined by the 'work' GameObject.
     * The calculations are based on the geometric relationships of the robot arm's segments.
     */
    public void IKTest()
    {
        // Constants
        const float AB = 0.169f;
        const float CD = 0.273f;
        const float HANDSIZE = 0.325f;
        const float GF = HANDSIZE - CD;
        const float FE = 0.49727f;
        const float ED = 0.70142f;

        // Work position
        Vector3 A = work.transform.localPosition;
        Debug.Log("Work position: " + A.ToString("F4"));

        float theta1 = Mathf.Atan2(A.z, A.x);
        Debug.Log("Theta1: " + (theta1 * Mathf.Rad2Deg).ToString("F4"));

        float AC = Mathf.Sqrt(A.x * A.x + A.z * A.z);
        float theta3 = Mathf.Asin(AB / AC);
        Debug.Log("Theta3: " + (theta3 * Mathf.Rad2Deg).ToString("F4"));

        float BC = AC * Mathf.Cos(theta3);
        Debug.Log("BC: " + BC.ToString("F4"));

        float theta2 = theta1 - theta3;
        Debug.Log("Theta2: " + (theta2 * Mathf.Rad2Deg).ToString("F4"));

        Vector3 B = new Vector3(BC * Mathf.Cos(theta2), A.y, BC * Mathf.Sin(theta2));
        Vector3 G = new Vector3(B.x, B.y + CD, B.z);

        float r = Mathf.Sqrt(BC * BC + GF * GF);
        Debug.Log("r: " + r.ToString("F4"));

        // Cosine theorem
        float theat6 = Mathf.Acos((FE * FE - ED * ED - r * r) / (-2 * ED * r));
        float theat7 = Mathf.Acos((r * r - FE * FE - ED * ED) / (-2 * FE * ED));
        Debug.Log("Theta6: " + (theat6 * Mathf.Rad2Deg).ToString("F4"));
        Debug.Log("Theta7: " + (theat7 * Mathf.Rad2Deg).ToString("F4"));

        float theat5 = Mathf.Atan2(GF, BC);
        float theat4 = theat5 + theat6;
        Debug.Log("Theta4: " + (theat4 * Mathf.Rad2Deg).ToString("F4"));
        Debug.Log("Theta5: " + (theat5 * Mathf.Rad2Deg).ToString("F4"));

        float theat8 = 3 * Mathf.PI / 2 - theat4 - theat7;
        Debug.Log("Theta8: " + (theat8 * Mathf.Rad2Deg).ToString("F4"));

        // Set rotations of the bones of the robot
        Debug.Log($"{initialSwingRotation.y}, {initialSwingRotation.z}");
        Debug.Log($"{initialBoomRotation.y}, {initialBoomRotation.z}");
        Debug.Log($"{initialArmRotation.y}, {initialArmRotation.z}");

        targetSwingRotation = initialSwingRotation * Quaternion.AngleAxis(-theta2 * Mathf.Rad2Deg, Vector3.up);
        targetBoomRotation = initialBoomRotation * Quaternion.AngleAxis(-theat4 * Mathf.Rad2Deg, Vector3.up);
        targetArmRotation = initialArmRotation * Quaternion.AngleAxis(theat7 * Mathf.Rad2Deg, Vector3.up);
        targetHandRotation = initialHandRotation * Quaternion.AngleAxis(theat8 * Mathf.Rad2Deg, Vector3.up);
    }
}
