using TMPro;
using UnityEngine;

namespace UnoGame.UI
{
    /// <summary>Makes each letter of a TextMeshPro label bob in a travelling wave (loading titles).</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class WaveText : MonoBehaviour
    {
        public float amplitude = 7f;
        public float speed = 6f;
        public float spread = 0.42f;
        TMP_Text text;

        void Awake() => text = GetComponent<TMP_Text>();

        void LateUpdate()
        {
            text.ForceMeshUpdate();
            var info = text.textInfo;
            float t = Time.unscaledTime * speed;
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                var verts = info.meshInfo[ch.materialReferenceIndex].vertices;
                float y = Mathf.Sin(t - i * spread) * amplitude;
                for (int j = 0; j < 4; j++) verts[ch.vertexIndex + j].y += y;
            }
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                var mesh = info.meshInfo[m].mesh;
                if (mesh == null) continue;
                mesh.vertices = info.meshInfo[m].vertices;
                text.UpdateGeometry(mesh, m);
            }
        }
    }
}
