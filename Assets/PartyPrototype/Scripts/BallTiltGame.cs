using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PartyPrototype
{
    // The ball is NOT parented to the board and receives no steering forces.
    // Gravity and contacts with the rotating kinematic platform do all the work.
    public sealed class BallTiltGame : MonoBehaviour
    {
        public RenderTexture View { get; private set; }
        public bool Running;
        public int Falls { get; private set; }
        public Action Finished;
        Rigidbody board, ball;
        Camera viewCamera;
        Vector2 neutral, tilt;
        bool completed, enabledSensor;
        readonly List<Material> materials = new List<Material>();
        static readonly Vector3 Origin = new Vector3(1000, 0, 0);
        public void Initialize()
        {
            var root = new GameObject("Tilting World"); root.transform.SetParent(transform);
            root.transform.position = Origin;
            board = root.AddComponent<Rigidbody>(); board.isKinematic = true; board.interpolation = RigidbodyInterpolation.Interpolate;
            Tile("Start lane", new Vector3(-3,0,-3), new Vector3(2,.4f,6), new Color32(64,73,85,255));
            Tile("Bridge", Vector3.zero, new Vector3(8,.4f,2), new Color32(22,169,159,255));
            Tile("Finish lane", new Vector3(3,0,3), new Vector3(2,.4f,6), new Color32(64,73,85,255));
            Tile("Start marker", new Vector3(-3,.21f,-5), new Vector3(1.7f,.02f,1), new Color32(235,234,182,255), false);
            Tile("Goal", new Vector3(3,.21f,5), new Vector3(1.7f,.02f,1.4f), new Color32(115,216,139,255), false);
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.name = "Free rolling ball";
            sphere.layer = 30; sphere.transform.SetParent(transform); sphere.transform.localScale = Vector3.one * .65f;
            // Direct type references preserve these native components in IL2CPP builds.
            var ballCollider = sphere.GetComponent<SphereCollider>();
            if (ballCollider == null) ballCollider = sphere.AddComponent<SphereCollider>();
            ballCollider.radius = .5f; ballCollider.isTrigger = false; ballCollider.enabled = true;
            sphere.GetComponent<Renderer>().sharedMaterial = Material(new Color32(235,234,182,255));
            ball = sphere.AddComponent<Rigidbody>(); ball.mass = 1; ball.interpolation = RigidbodyInterpolation.Interpolate;
            ball.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            ball.maxAngularVelocity = 35;
            var camObject = new GameObject("Ball Tilt Camera"); camObject.transform.SetParent(transform);
            viewCamera = camObject.AddComponent<Camera>(); viewCamera.cullingMask = 1 << 30;
            viewCamera.transform.position = Origin + new Vector3(0,14,-10);
            viewCamera.transform.LookAt(Origin); viewCamera.orthographic = true; viewCamera.orthographicSize = 8.4f;
            viewCamera.clearFlags = CameraClearFlags.SolidColor; viewCamera.backgroundColor = new Color32(29,28,42,255);
            View = new RenderTexture(640,800,24); View.Create(); viewCamera.targetTexture = View;
            if (Accelerometer.current != null)
            { enabledSensor = !Accelerometer.current.enabled; InputSystem.EnableDevice(Accelerometer.current); }
            Calibrate(); Respawn();
        }
        Material Material(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var template = Resources.Load<Material>("PartyTiltMaterial");
            var material = template != null ? new Material(template) : new Material(shader); material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            materials.Add(material); return material;
        }
        void Tile(string name, Vector3 position, Vector3 size, Color color, bool collide = true)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube); tile.name = name; tile.layer = 30;
            var floorCollider = tile.GetComponent<BoxCollider>();
            if (floorCollider == null) floorCollider = tile.AddComponent<BoxCollider>();
            floorCollider.enabled = collide; floorCollider.isTrigger = false;
            tile.transform.SetParent(board.transform, false); tile.transform.localPosition = position; tile.transform.localScale = size;
            tile.GetComponent<Renderer>().sharedMaterial = Material(color);
        }
        public void Calibrate()
        {
            if (Accelerometer.current != null)
            { var a = Accelerometer.current.acceleration.ReadValue(); neutral = new Vector2(a.x, a.y); }
        }
        public void Retry() { if (!completed) { Falls++; Respawn(); } }
        void Respawn()
        {
            board.rotation = Quaternion.identity; tilt = Vector2.zero;
            bool wasKinematic = ball.isKinematic; ball.isKinematic = false;
            ball.position = Origin + new Vector3(-3,.8f,-5); ball.linearVelocity = Vector3.zero; ball.angularVelocity = Vector3.zero;
            ball.isKinematic = wasKinematic;
        }
        void FixedUpdate()
        {
            if (ball == null) return;
            ball.isKinematic = !Running || completed;
            if (!Running || completed) return;
            Vector2 input = Vector2.zero;
            if (Application.isMobilePlatform && Accelerometer.current != null)
            { var a = Accelerometer.current.acceleration.ReadValue(); input = (new Vector2(a.x,a.y) - neutral) * 2.5f; }
            var k = Keyboard.current;
            if (k != null)
            {
                input.x += (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
                input.y += (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0);
            }
            tilt = Vector2.MoveTowards(tilt, Vector2.ClampMagnitude(input,1) * 14, 45 * Time.fixedDeltaTime);
            board.MoveRotation(Quaternion.Euler(tilt.y,0,-tilt.x));
            if (ball.position.y < -4) { Retry(); return; }
            var local = board.transform.InverseTransformPoint(ball.position);
            if (Mathf.Abs(local.x - 3) < .78f && Mathf.Abs(local.z - 5) < .65f && local.y > .2f && local.y < 1)
            { completed = true; ball.isKinematic = true; Finished?.Invoke(); }
        }
        void OnDestroy()
        {
            if (enabledSensor && Accelerometer.current != null) InputSystem.DisableDevice(Accelerometer.current);
            if (viewCamera != null) viewCamera.targetTexture = null;
            if (View != null) { View.Release(); Destroy(View); }
            foreach (var material in materials) Destroy(material);
        }
    }
}
