using System.Collections.Generic;
using UnityEngine;

public enum EnemyWaypointKind { Hallway, Stair, Landing, UpperPort, LowerPort }
public sealed class EnemyWaypoint : MonoBehaviour
{
    public EnemyWaypointKind Kind { get; set; }
    public List<EnemyWaypoint> Connections { get; } = new();
    public void ConnectBidirectional(EnemyWaypoint other) { if (other == null || other == this) return; if (!Connections.Contains(other)) Connections.Add(other); if (!other.Connections.Contains(this)) other.Connections.Add(this); }
    public void Disconnect(EnemyWaypoint other) { Connections.Remove(other); if (other != null) other.Connections.Remove(this); }
}
