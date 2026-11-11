using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace RoadsideHaven.Tests
{
    /// <summary>Checks parking pedestrian routes against the actual generated navigation asset.</summary>
    public sealed class ParkingNavigationTests
    {
        [TestCase(-7.2f)]
        [TestCase(-4.1f)]
        [TestCase(-.5f)]
        [TestCase(3.1f)]
        public void ParkingBayConnectsToEveryReceptionSlotAndBack(float bayX)
        {
            var data = AssetDatabase.LoadAssetAtPath<NavMeshData>(
                "Assets/_Game/Generated/PrototypeNavMesh.asset");
            Assert.That(data, Is.Not.Null, "Generate the prototype first.");
            var instance = NavMesh.AddNavMeshData(data);
            try
            {
                Assert.That(NavMesh.SamplePosition(new Vector3(bayX, .15f, -7.8f), out var parking,
                    .7f, NavMesh.AllAreas), Is.True, "Parking point must be close to the baked courtyard.");
                for (int slot = 0; slot < 8; slot++)
                {
                    Assert.That(NavMesh.SamplePosition(new Vector3(-6 + slot * 1.45f, .2f, -1.9f),
                        out var queue, .7f, NavMesh.AllAreas), Is.True);
                    AssertCompletePath(parking.position, queue.position);
                    AssertCompletePath(queue.position, parking.position);
                }
            }
            finally { if (instance.valid) NavMesh.RemoveNavMeshData(instance); }
        }

        private static void AssertCompletePath(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete),
                $"Expected a complete pedestrian route from {from} to {to}.");
        }
    }
}
