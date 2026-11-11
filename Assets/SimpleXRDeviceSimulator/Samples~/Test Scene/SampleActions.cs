using UnityEngine;

namespace SimpleXRDeviceSimulator.Sample
{
    /// <summary>
    /// The sample's Reset button: puts the cube back where it started, at its starting size.
    /// </summary>
    public class SampleActions : MonoBehaviour
    {
        [SerializeField] Transform cube;

        Vector3 startPosition;
        Quaternion startRotation;
        Vector3 startScale;

        void Awake()
        {
            startPosition = cube.position;
            startRotation = cube.rotation;
            startScale = cube.localScale;
        }

        public void ResetCube()
        {
            cube.SetPositionAndRotation(startPosition, startRotation);
            cube.localScale = startScale;
        }
    }
}
