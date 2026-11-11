using UnityEngine;
using TMPro;

namespace RoadsideHaven
{
    /// <summary>Scene marker that links room visuals to their runtime room model.</summary>
    public sealed class RoomSelectable : MonoBehaviour
    {
        [SerializeField] private string roomId;
        [SerializeField] private Transform entryPoint;
        private TMP_Text _statusLabel;

        public string RoomId => roomId;
        public Transform EntryPoint => entryPoint;

        /// <summary>Updates the existing generated room sign without changing shared materials.</summary>
        public void RefreshStatus(HotelRoom room, bool selected, float stayRemaining)
        {
            if (_statusLabel == null)
                _statusLabel = GetComponentInChildren<TextMeshPro>(true);
            if (_statusLabel == null || room == null) return;

            string status = room.State.ToString();
            if (room.State == RoomState.Cleaning)
                status += $" {Mathf.CeilToInt(room.CleaningRemaining)}s";
            else if (room.State == RoomState.Occupied)
                status += $" {Mathf.CeilToInt(Mathf.Max(0f, stayRemaining))}s";
            _statusLabel.text = $"{(selected ? "> " : string.Empty)}{room.Id}\n<size=55%>{status}</size>";
            _statusLabel.color = selected ? new Color(1f, .55f, .12f) :
                room.State == RoomState.Available ? new Color(.45f, .93f, .66f) :
                room.State == RoomState.Dirty ? new Color(1f, .5f, .3f) :
                new Color(.75f, .85f, 1f);
        }

        public void Configure(string id, Transform roomEntry)
        {
            roomId = id;
            entryPoint = roomEntry;
        }
    }

}

