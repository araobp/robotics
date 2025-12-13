using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;

// このクラスはロボットアームのインバースキネマティクス（IK）を実装し、ターゲットに到達するために必要な関節角度を計算し、ロボットの動きをスムーズにアニメーションさせます。
public class RobotTest : MonoBehaviour
{
    // --- フィールド ---

    // ロボットアームのエンドエフェクタが到達しようとするターゲットのGameObject。
    [Header("IK Target")]
    [SerializeField] GameObject work;

    // ロボットの関節を表すGameObject。
    [Header("Robot Joints")]
    [SerializeField] GameObject swingAxis;
    [SerializeField] GameObject boomAxis;
    [SerializeField] GameObject armAxis;
    [SerializeField] GameObject handAxis;

    // インバースキネマティクスの動作設定。
    [Header("IK Settings")]
    [Tooltip("The duration in seconds for the IK movement to complete.")]
    [SerializeField] private float ikMoveDuration = 1.0f;

    // 進行中の非同期移動タスクをキャンセルするためのトークンソース。
    private CancellationTokenSource _ikMoveCts;

    // 各関節の初期回転を保存し、相対的な計算を可能にします。
    Quaternion initialSwingRotation;
    Quaternion initialBoomRotation;
    Quaternion initialArmRotation;
    Quaternion initialHandRotation;

    // 最初のフレーム更新の前に一度だけ呼び出されます。ロボットの関節の初期回転を初期化し、最初のIK計算をスケジュールします。
    void Start()
    {
        // 各ボーンの初期ローカル回転をキャプチャします。
        initialSwingRotation = swingAxis.transform.localRotation;
        initialBoomRotation = boomAxis.transform.localRotation;
        initialArmRotation = armAxis.transform.localRotation;
        initialHandRotation = handAxis.transform.localRotation;

        // ロボットアームの初期ポーズを設定します。
        setPose(Mathf.PI/2, Mathf.PI/2, Mathf.PI/2, Mathf.PI/2);

        // 2秒後にIKTestメソッドを呼び出し、IKプロセスを開始します。
        Invoke("IKTest", 2f);
    }

    // MonoBehaviourが破棄されるときに呼び出されます。
    void OnDestroy()
    {
        // メモリリークを防ぐために、CancellationTokenSourceがキャンセルされ、破棄されることを保証します。
        _ikMoveCts?.Cancel();
        _ikMoveCts?.Dispose();
    }

    /* この関数は、ロボットアームのインバースキネマティクス（IK）のコア実装です。
     * エンドエフェクタ（手）を 'work' GameObjectで定義されたターゲット位置に配置するために必要な
     * 関節角度（theta値）を計算します。
     * この計算は、ロボットアームのセグメントの幾何学的関係に基づいており、
     * 2D平面IK問題を解いています。
     */
    public async void IKTest()
    {
        // ロボットアームの各セグメントの固定長を定義します。
        const float AB = 0.169f;
        const float CD = 0.273f;
        const float HANDSIZE = 0.325f;
        const float GF = HANDSIZE - CD;
        const float FE = 0.49727f;
        const float ED = 0.70142f;

        // 'work' GameObjectからターゲット位置をローカル空間で取得し、幾何計算を実行します。
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

        // 余弦定理を使用して、アームの三角形の内部角度を求めます。
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

        // デバッグのために初期回転値をログに出力します。
        Debug.Log($"{initialSwingRotation.y}, {initialSwingRotation.z}");
        Debug.Log($"{initialBoomRotation.y}, {initialBoomRotation.z}");
        Debug.Log($"{initialArmRotation.y}, {initialArmRotation.z}");

        // 新しい移動タスクを開始する前に、既存のタスクをキャンセルします。
        if (_ikMoveCts != null)
        {
            _ikMoveCts.Cancel();
            _ikMoveCts.Dispose();
        }

        // 新しいCancellationTokenSourceを作成し、移動タスクを開始します。
        _ikMoveCts = new CancellationTokenSource();
        var newTargets = CalculateTargetPose(theta2, theat4, theat7, theat8);
        await MoveToTargets(newTargets.swing, newTargets.boom, newTargets.arm, newTargets.hand, ikMoveDuration, _ikMoveCts.Token);
    }

    // ロボットアームの関節のポーズを指定された角度に直接設定します。
    void setPose(float swingAngle, float boomAngle, float armAngle, float handAngle)
    {
        // 各関節の初期向きに対して相対的に回転を適用します。
        swingAxis.transform.localRotation = initialSwingRotation * Quaternion.AngleAxis(swingAngle * Mathf.Rad2Deg, -Vector3.up);
        boomAxis.transform.localRotation = initialBoomRotation * Quaternion.AngleAxis(boomAngle * Mathf.Rad2Deg, -Vector3.up);
        armAxis.transform.localRotation = initialArmRotation * Quaternion.AngleAxis(armAngle * Mathf.Rad2Deg, Vector3.up);
        handAxis.transform.localRotation = initialHandRotation * Quaternion.AngleAxis(handAngle * Mathf.Rad2Deg, Vector3.up);

        // ポーズを直接設定する場合、Update()による意図しないスムーズな動きを防ぐために、ターゲットの回転も更新します。
        var newTargets = CalculateTargetPose(swingAngle, boomAngle, armAngle, handAngle);
    }

    // IKソルバーで計算されたターゲットの回転を計算します。
    (Quaternion swing, Quaternion boom, Quaternion arm, Quaternion hand) CalculateTargetPose(float swingAngle, float boomAngle, float armAngle, float handAngle)
    {
        // 各関節のターゲット回転を、初期の向きに対して相対的に計算します。
        var swing = initialSwingRotation * Quaternion.AngleAxis(swingAngle * Mathf.Rad2Deg, -Vector3.up);
        var boom = initialBoomRotation * Quaternion.AngleAxis(boomAngle * Mathf.Rad2Deg, -Vector3.up);
        var arm = initialArmRotation * Quaternion.AngleAxis(armAngle * Mathf.Rad2Deg, Vector3.up);
        var hand = initialHandRotation * Quaternion.AngleAxis(handAngle * Mathf.Rad2Deg, Vector3.up);
        return (swing, boom, arm, hand);
    }

    // 指定された時間をかけて、ロボットの関節を現在の回転からターゲットの回転まで非同期でスムーズに動かします。
    private async Task MoveToTargets(Quaternion swingTarget, Quaternion boomTarget, Quaternion armTarget, Quaternion handTarget, float duration, CancellationToken cancellationToken)
    {
        // 各関節の開始時の回転をキャプチャします。
        Quaternion startSwing = swingAxis.transform.localRotation;
        Quaternion startBoom = boomAxis.transform.localRotation;
        Quaternion startArm = armAxis.transform.localRotation;
        Quaternion startHand = handAxis.transform.localRotation;
        float elapsedTime = 0f;
        
        try
        {
            // 経過時間が指定された時間に達するまでループします。
            while (elapsedTime < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Slerpを使用して各関節の回転を補間します。
                float t = elapsedTime / duration;
                swingAxis.transform.localRotation = Quaternion.Slerp(startSwing, swingTarget, t);
                boomAxis.transform.localRotation = Quaternion.Slerp(startBoom, boomTarget, t);
                armAxis.transform.localRotation = Quaternion.Slerp(startArm, armTarget, t);
                handAxis.transform.localRotation = Quaternion.Slerp(startHand, handTarget, t);
                
                elapsedTime += Time.deltaTime;
                // 次のフレームまで待機してからループを続行します。
                await Task.Yield();
            }

            // タイミングのわずかな不正確さがあった場合に備え、最終的な回転が正確にターゲットに設定されるようにします。
            swingAxis.transform.localRotation = swingTarget;
            boomAxis.transform.localRotation = boomTarget;
            armAxis.transform.localRotation = armTarget;
            handAxis.transform.localRotation = handTarget;
        }
        catch (TaskCanceledException)
        {
            // タスクがキャンセルされました。これは新しい移動命令が出されたときの予期された動作です。
        }
    }
}
