using System;
using GorillaLocomotion;
using GorillaRichPresence.Behaviours;
using GorillaRichPresence.Tools;
using UnityEngine;
using Zenject;

namespace GorillaRichPresence
{
    // Token: 0x0200003E RID: 62
    public class RP_Installer : Installer
    {
        // Token: 0x06000136 RID: 310 RVA: 0x000068C8 File Offset: 0x00004AC8
        public override void InstallBindings()
        {
            base.Container.BindInterfacesAndSelfTo<RP_Core>().FromNewComponentOn((InjectContext ctx) => UnityEngine.Object.FindObjectOfType<Player>().gameObject).AsSingle();
            base.Container.BindInterfacesAndSelfTo<Logging>().AsSingle();
            base.Container.Bind<Configuration>().AsSingle();
            base.Container.Bind<DiscordRegistrar>().AsSingle();
        }
    }
}