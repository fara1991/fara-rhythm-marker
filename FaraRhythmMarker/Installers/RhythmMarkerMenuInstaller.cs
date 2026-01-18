using FaraRhythmMarker.Views;
using Zenject;

namespace FaraRhythmMarker.Installers
{
    internal class RhythmMarkerMenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesTo<SettingsMenuManager>().AsSingle();
        }
    }
}
