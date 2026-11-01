using UnityEngine;
using Object=UnityEngine.Object;

namespace PinchOfTheft.Editor
{
    // Worktable geometry only. Uses project materials without cloning or changing them.
    public static class IntroVanAccessGeometry
    {
        public static GameObject Cube(string name,Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localScale=scale;obj.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(obj.GetComponent<Collider>());return obj;
        }
        public static float SurfaceBelow(MeshFilter body,Vector3 point,float ceiling)
        {
            var vertices=body.sharedMesh.vertices;var triangles=body.sharedMesh.triangles;float best=float.NegativeInfinity;
            for(int i=0;i<triangles.Length;i+=3)
            {
                Vector3 a=body.transform.TransformPoint(vertices[triangles[i]]),b=body.transform.TransformPoint(vertices[triangles[i+1]]),c=body.transform.TransformPoint(vertices[triangles[i+2]]);
                float determinant=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(determinant)<1e-8f)continue;
                float u=((b.z-c.z)*(point.x-c.x)+(c.x-b.x)*(point.z-c.z))/determinant;
                float v=((c.z-a.z)*(point.x-c.x)+(a.x-c.x)*(point.z-c.z))/determinant;
                float w=1-u-v;float y=u*a.y+v*b.y+w*c.y;
                if(u>=-.0001f&&v>=-.0001f&&w>=-.0001f&&y<ceiling&&y>best)best=y;
            }
            return float.IsNegativeInfinity(best)?.80f:best;
        }
    }
}
