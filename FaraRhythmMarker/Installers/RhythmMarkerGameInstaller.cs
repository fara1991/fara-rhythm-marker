using FaraRhythmMarker.Controllers;
using Zenject;

namespace FaraRhythmMarker.Installers
{
    internal class RhythmMarkerGameInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<RhythmMarkerController>().AsSingle();
        }
    }
}
