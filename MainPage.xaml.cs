using SharpDX;
using SoftEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace SoftEngine
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private Device device;
        Mesh[] meshes;
        Camera mera = new Camera();
        float frameCount = 0;
        float bobbingHeight = 0.01f; // Adjust this value to control the bobbing height

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            //backBuffer resolution 
            WriteableBitmap bmp = new WriteableBitmap(640, 480);

            device = new Device(bmp);

            //XAML Image control
            frontBuffer.Source = bmp;

            // load mesh from JSON file 
            meshes = await device.LoadJSONFileAsync("monkey.babylon");

            // initialize camera values
            mera.Position = new Vector3(0, 0, 10.0f);
<<<<<<< HEAD
            mera.Target =  Vector3.Zero;
=======
            mera.Target = Vector3.Zero;
>>>>>>> version-4

            // Registering to the XAML rendering loop
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        // Rendering loop handler
        void CompositionTarget_Rendering(object sender, object e)
        {
            device.Clear(0, 0, 0, 255);

            // Calculate vertical displacement (bobbing)
            float displacement = (float)Math.Sin(frameCount * 0.1f) * bobbingHeight;

            // Update the position of the camera to create the bobbing effect
            Vector3 cameraPosition = mera.Target;
            cameraPosition.Y += displacement;
            mera.Target = cameraPosition;


            foreach (var mesh in meshes)
            {
                // rotating slightly the meshes during each frame rendered
                mesh.Rotation = new Vector3(mesh.Rotation.X, mesh.Rotation.Y + 0.01f, mesh.Rotation.Z);
            }
            
            // Doing the various matrix operations
            device.Render(mera, meshes);
            // Flushing the back buffer into the front buffer
            device.Present();
            // Increment the frame count for animation
            frameCount += 0.5f;
        }

        public MainPage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }
    }
}
