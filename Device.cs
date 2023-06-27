using Windows.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SoftEngine
{
    public class Device
    {
        private byte[] backBuffer;
        private readonly float[] depthBuffer;
        private WriteableBitmap bmp;
        private readonly int renderWidth;
        private readonly int renderHeight;

        public Device(WriteableBitmap bmp)
        {
            this.bmp = bmp;
            renderWidth = bmp.PixelWidth;
            renderHeight = bmp.PixelHeight;
            // the back buffer size is equal to the number of pixels to draw
            // on screen (width*height) * 4 (R,G,B & Alpha values). 
            backBuffer = new byte[bmp.PixelWidth * bmp.PixelHeight * 4];
            depthBuffer = new float[bmp.PixelWidth * bmp.PixelHeight];
        }

        // This method is called to clear the back buffer with a specific color
        public void Clear(byte r, byte g, byte b, byte a)
        {
            // clearing backBuffer
            for (var index = 0; index < backBuffer.Length; index += 4)
            {
                // BGRA is used by Windows
                backBuffer[index] = b;
                backBuffer[index + 1] = g;
                backBuffer[index + 2] = r;
                backBuffer[index + 3] = a;
            }
            // clearing depthBuffer
            for (var index = 0; index < depthBuffer.Length; index++)
            {
                depthBuffer[index] = float.MaxValue;
            }
        }

        // Once everything is ready, we can flush the back buffer
        // into the front buffer. 
        public void Present()
        {
            using (var stream = bmp.PixelBuffer.AsStream())
            {
                // writing our byte[] back buffer into our WriteableBitmap stream
                stream.Write(backBuffer, 0, backBuffer.Length);
            }
            // request a redraw of the entire bitmap
            bmp.Invalidate();
        }

        // Called to put a pixel on screen at a specific X,Y coordinates
        public void PutPixel(int x, int y, float z, Color4 color)
        {
            // As we have a 1-D Array for our back buffer
            // we need to know the equivalent cell in 1-D based
            // on the 2D coordinates on screen
            var index = (x + y * renderWidth);
            var index4 = index * 4;

            if (depthBuffer[index] < z)
            {
                return; // discard
            }

            depthBuffer[index] = z;

            backBuffer[index4] = (byte)(color.Blue * 255);
            backBuffer[index4 + 1] = (byte)(color.Green * 255);
            backBuffer[index4 + 2] = (byte)(color.Red * 255);
            backBuffer[index4 + 3] = (byte)(color.Alpha * 255);
        }

        // Project takes some 3D coordinates and transform them
        // in 2D coordinates using the transformation matrix
        public Vector3 Project(Vector3 coord, Matrix transMat)
        {
            // transforming the coordinates
            var point = Vector3.TransformCoordinate(coord, transMat);
            // transform coordinates relative to the bitmap
            var x = point.X * bmp.PixelWidth + bmp.PixelWidth / 2.0f;
            var y = -point.Y * bmp.PixelHeight + bmp.PixelHeight / 2.0f;
            return (new Vector3(x, y, point.Z));
        }

        // DrawPoint calls PutPixel but does the clipping operation before
        public void DrawPoint(Vector3 point, Color4 color)
        {
            // Clipping what's visible on screen
            if (point.X >= 0 && point.Y >= 0 && point.X < bmp.PixelWidth && point.Y < bmp.PixelHeight)
            {
                // Drawing a point
                PutPixel((int)point.X, (int)point.Y, point.Z, color);
            }
        }

        // "Clamps" values keeping them between 1 & 0
        float Clamp(float value, float min = 0, float max = 1)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        // interpolating the value between 2 vertices
        // min is starting point, max is end point
        // gradient is the % between the 2 points
        float Interpolate(float min, float max, float gradient)
        {
            return min + (max - min) * Clamp(gradient);
        }

        void ProcessScanLine(int y, Vector3 pa, Vector3 pb, Vector3 pc, Vector3 pd, Color4 color)
        {
            var gradient1 = pa.Y != pb.Y ? (y - pa.Y) / (pb.Y - pa.Y) : 1;
            var gradient2 = pc.Y != pd.Y ? (y - pc.Y) / (pd.Y - pc.Y) : 1;

            int sx = (int)Interpolate(pa.X, pb.X, gradient1);
            int ex = (int)Interpolate(pc.X, pd.X, gradient2);

            // starting & ending Z
            float z1 = Interpolate(pa.Z, pb.Z, gradient1);
            float z2 = Interpolate(pc.Z, pd.Z, gradient2);

            // drawing a line from left (sx) to right (ex)
            for (var x = sx; x < ex; x++)
            {
                float gradient = (x - sx) / (float)(ex - sx);

                var z = Interpolate(z1, z2, gradient);
                DrawPoint(new Vector3(x, y, z), color);
            }
        }

        public void DrawTriangle(Vector3 p1, Vector3 p2, Vector3 p3, Color4 color)
        {
            // sorting points so they are always in order of p1, p2 & p3
            // p1 will always be up (meaning its Y is the lowest possible
            // & p2 will always be between p1 & p3
            if (p1.Y > p2.Y)
            {
                var temp = p2;
                p2 = p1;
                p1 = temp;
            }
            if (p2.Y > p3.Y)
            {
                var temp = p2;
                p2 = p3;
                p3 = temp;
            }
            if (p1.Y > p2.Y)
            {
                var temp = p2;
                p2 = p1;
                p1 = temp;
            }
            // inverse slopes
            float dP1P2, dP1P3;
            // computing inverse slopes
            if (p2.Y - p1.Y > 0)
                dP1P2 = (p2.X - p1.X) / (p2.Y - p1.Y);
            else
                dP1P2 = 0;

            if (p3.Y - p1.Y > 0)
                dP1P3 = (p3.X - p1.X) / (p3.Y - p1.Y);
            else
                dP1P3 = 0;

            // first case triangles, p2 is on the right
            // P1
            // -
            // -- 
            // - -
            // -  -
            // -   - P2
            // -  -
            // - -
            // -
            // P3
            if (dP1P2 > dP1P3)
            {
                for (var y = (int)p1.Y; y <= (int)p3.Y; y++)
                {
                    if (y < p2.Y)
                    {
                        ProcessScanLine(y, p1, p3, p1, p2, color);
                    }
                    else
                    {
                        ProcessScanLine(y, p1, p3, p2, p3, color);
                    }
                }
            }
            // second case triangles, p2 is on the left
            //       P1
            //        -
            //       -- 
            //      - -
            //     -  -
            // P2 -   - 
            //     -  -
            //      - -
            //        -
            //       P3
            else
            {
                for (var y = (int)p1.Y; y <= (int)p3.Y; y++)
                {
                    if (y < p2.Y)
                    {
                        ProcessScanLine(y, p1, p2, p1, p3, color);
                    }
                    else
                    {
                        ProcessScanLine(y, p2, p3, p1, p3, color);
                    }
                }
            }
        }



        // The main method of the engine that re-compute each vertex projection
        // during each frame
        public void Render(Camera camera, params Mesh[] meshes)
        {
            var viewMatrix = Matrix.LookAtLH(camera.Position, camera.Target, Vector3.UnitY);
            var projectionMatrix = Matrix.PerspectiveFovRH(0.78f,
                                                           (float)bmp.PixelWidth / bmp.PixelHeight,
                                                           0.01f, 1.0f);

            foreach (Mesh mesh in meshes)
            {
                var worldMatrix = Matrix.RotationYawPitchRoll(mesh.Rotation.Y, mesh.Rotation.X, mesh.Rotation.Z) *
                                   Matrix.Translation(mesh.Position);

                var transformMatrix = worldMatrix * viewMatrix * projectionMatrix;

                // drawing faces
                var faceIndex = 0;
                foreach (var face in mesh.Faces)
                {
                    var vertexA = mesh.Vertices[face.A];
                    var vertexB = mesh.Vertices[face.B];
                    var vertexC = mesh.Vertices[face.C];

                    var pixelA = Project(vertexA, transformMatrix);
                    var pixelB = Project(vertexB, transformMatrix);
                    var pixelC = Project(vertexC, transformMatrix);

                    var color = 0.25f + (faceIndex % mesh.Faces.Length) * 0.75f / mesh.Faces.Length;
                    DrawTriangle(pixelA, pixelB, pixelC, new Color4(color, color, color, 1));
                    faceIndex++;
                }
            }
        }

        // Loading JSON file in asynchronous manner
        public async Task<Mesh[]> LoadJSONFileAsync(string fileName)
        {
            var meshes = new List<Mesh>();
            var file = await
                Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync(fileName);
            var data = await Windows.Storage.FileIO.ReadTextAsync(file);
            dynamic jsonobject =
                Newtonsoft.Json.JsonConvert.DeserializeObject(data);

            for (var meshIndex = 0; meshIndex < jsonobject.meshes.Count; meshIndex++)
            {
                var meshData = jsonobject.meshes[meshIndex];

                // Vertices
                var positions = meshData.positions;
                var verticesStep = 3; // Number of coordinates per vertex

                // UV coordinates
                var uvData = meshData.uvs;
                var uvCount = uvData != null ? uvData.Count : 0;

                // Depending on the number of texture coordinates per vertex
                // we determine the number of elements per vertex
                switch (uvCount)
                {
                    case 0:
                        verticesStep = 3; // Only positions
                        break;
                    case 1:
                        verticesStep = 4; // Positions + 1 set of UV coordinates
                        break;
                    case 2:
                        verticesStep = 5; // Positions + 2 sets of UV coordinates
                        break;
                }

                // The number of interesting vertices information
                var verticesCount = positions.Count / verticesStep;

                // Faces
                var indices = meshData.indices;
                var facesCount = indices.Count / 3;

                var mesh = new Mesh((string)meshData.name, (int)verticesCount, (int)facesCount);

                // filling the vertices array of our mesh first
                for (var index = 0; index < verticesCount; index++)
                {
                    var x = (float)positions[index * verticesStep].Value;
                    var y = (float)positions[index * verticesStep + 1].Value;
                    y = -y;
                    var z = (float)positions[index * verticesStep + 2].Value;
                    mesh.Vertices[index] = new Vector3(x, y, z);
                }

                // then filling faces array
                for (var index = 0; index < facesCount; index++)
                {
                    var a = (int)indices[index * 3].Value;
                    var b = (int)indices[index * 3 + 1].Value;
                    var c = (int)indices[index * 3 + 2].Value;
                    mesh.Faces[index] = new Face { A = a, B = b, C = c };
                }
                // getting the position set in blender
                var position = jsonobject.meshes[meshIndex].position;
                mesh.Position = new Vector3((float)position[0].Value,
                    (float)position[1].Value, (float)position[2].Value);
                meshes.Add(mesh);
            }
            return meshes.ToArray();
        }
    }
}