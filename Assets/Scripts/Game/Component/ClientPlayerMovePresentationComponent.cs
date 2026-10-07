using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// 本地玩家移动的纯表现状态。该组件不进入预测快照，也不会被 SS 回滚。
/// </summary>
public struct ClientPlayerMovePresentationComponent : IComponentData
{
    public float3 CurrentPosition;
    public float3 CorrectionOffset;
    public float3 PreviousPredictionPosition;
    public float3 LatestPredictionPosition;
    public float3 ReconciliationFromPosition;
    public byte ReconciliationPending;
    public byte Initialized;
    public byte HasPredictionSamples;

    public void CompleteReconciliation(float3 predictedPosition)
    {
        if (ReconciliationPending == 0)
            return;
        // Smooth only the correction, never the movement of the next normal tick.
        float3 correction = ReconciliationFromPosition - predictedPosition;
        CorrectionOffset += correction;
        PreviousPredictionPosition -= correction;
        LatestPredictionPosition = predictedPosition;
        if (math.lengthsq(CorrectionOffset) >= 36f)
        {
            CorrectionOffset = float3.zero;
            PreviousPredictionPosition = predictedPosition;
        }
        ReconciliationPending = 0;
    }
}
