﻿using Windows.UI.Xaml.Media.Imaging;
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
        private WriteableBitmap bmp;

        public Device(WriteableBitmap bmp)
        {
            this.bmp = bmp;
            // the back buffer size is equal to the number of pixels to draw
            // on screen (width*height) * 4 (R,G,B & Alpha values). 
            backBuffer = new byte[bmp.PixelWidth * bmp.PixelHeight * 4];
        }

        // This method is called to clear the back buffer with a specific color
        public void Clear(byte r, byte g, byte b, byte a)
        {
            for (var index = 0; index < backBuffer.Length; index += 4)
            {
                // BGRA is used by Windows
                backBuffer[index] = b;
                backBuffer[index + 1] = g;
                backBuffer[index + 2] = r;
                backBuffer[index + 3] = a;
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
        public void PutPixel(int x, int y, Color4 color)
        {
            // As we have a 1-D Array for our back buffer
            // we need to know the equivalent cell in 1-D based
            // on the 2D coordinates on screen
            var index = (x + y * bmp.PixelWidth) * 4;

            backBuffer[index] = (byte)(color.Blue * 255);
            backBuffer[index + 1] = (byte)(color.Green * 255);
            backBuffer[index + 2] = (byte)(color.Red * 255);
            backBuffer[index + 3] = (byte)(color.Alpha * 255);
        }

        // Project takes some 3D coordinates and transform them
        // in 2D coordinates using the transformation matrix
        public Vector2 Project(Vector3 coord, Matrix transMat)
        {
            // transforming the coordinates
            var point = Vector3.TransformCoordinate(coord, transMat);
            // transform coordinates relative to the bitmap
            var x = point.X * bmp.PixelWidth + bmp.PixelWidth / 2.0f;
            var y = -point.Y * bmp.PixelHeight + bmp.PixelHeight / 2.0f;
            return (new Vector2(x, y));
        }

        // DrawPoint calls PutPixel but does the clipping operation before
        public void DrawPoint(Vector2 point)
        {
            // Clipping what's visible on screen
            if (point.X >= 0 && point.Y >= 0 && point.X < bmp.PixelWidth && point.Y < bmp.PixelHeight)
            {
                // Drawing a yellow point
                PutPixel((int)point.X, (int)point.Y, new Color4(1.0f, 1.0f, 0.0f, 1.0f));
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
                var worldMatrix = Matrix.RotationYawPitchRoll(mesh.Rotation.Y, mesh.Rotation.X - MathF.PI / 2, mesh.Rotation.Z) *
                                  Matrix.Scaling(1.5f) * Matrix.Translation(mesh.Position);

                var transformMatrix = worldMatrix * viewMatrix * projectionMatrix;

                // drawing faces
                foreach (var face in mesh.Faces)
                {
                    var vertexA = mesh.Vertices[face.A];
                    var vertexB = mesh.Vertices[face.B];
                    var vertexC = mesh.Vertices[face.C];

                    var pixelA = Project(vertexA, transformMatrix);
                    var pixelB = Project(vertexB, transformMatrix);
                    var pixelC = Project(vertexC, transformMatrix);

                    DrawBline(pixelA, pixelB);
                    DrawBline(pixelB, pixelC);
                    DrawBline(pixelC, pixelA);
                }
            }
        }
        // drawing line with midpoints
        public void DrawLine(Vector2 point0, Vector2 point1)
        {
            var dist = (point1 - point0).Length();

            if (dist < 2)
                return;
            // find middle point between both points
            Vector2 middlePoint = point0 + (point1 - point0) / 2;
            // then draw the middlepoint on screen
            DrawPoint(middlePoint);
            // recursive algarithm to draw from both sides
            DrawLine(point0, middlePoint);
            DrawLine(middlePoint, point1);
        }

        // more efficient line drawing algorithm (Bresenham's line algorithm)
        public void DrawBline(Vector2 point0, Vector2 point1)
        {
            // first determin the start and end points of the line segment
            int x0 = (int)point0.X;
            int y0 = (int)point0.Y;
            int x1 = (int)point1.X;
            int y1 = (int)point1.Y;

            // calculate the difference in the x & y as dx & dy 
            var dx = Math.Abs(x1 - x0);
            var dy = Math.Abs(y1 - y0);
            // calculate the sign of these differences
            var sx = (x0 < x1) ? 1 : -1;
            var sy = (y0 < y1) ? 1 : -1;
            // Calculate the initial error value (needed for Bresenham's)
            var err = dx - dy;

            while (true)
            {
                // draw current point
                DrawPoint(new Vector2(x0, y0));

                // check if end point is reached
                if ((x0 == x1) && (y0 == y1)) break;
                // calculate err doubled  
                var e2 = 2 * err;
                // Adjust the error value and move along the x-axis if necessary
                if (e2 > -dy) { err -= dy; x0 += sx; }
                // Adjust the error value and move along the y-axis if necessary
                if (e2 < dx) { err += dx; y0 += sy; }
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