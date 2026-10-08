using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace AbyssalProtocol
{
    public static class AbyssalAutoStart
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureCampaign()
        {
            if (Object.FindObjectOfType<AbyssalManager>() == null)
                new GameObject("Campaign Bootstrap").AddComponent<AbyssalManager>();
        }
    }

    // Component attached to Campaign Bootstrap. The campaign is built at runtime.
    public class AbyssalManager : MonoBehaviour
    {
        public static AbyssalManager Instance;
        public int level = 1, health = 100, ammo = 24, reserve = 96, terminals, kills;
        public bool redKey, reactorOff, ended, victory;
        public float escapeTimer;
        Transform world, player;
        Camera view;
        GUIStyle hudStyle, centerStyle;
        string notice = "";
        float noticeUntil, nextShot, pitch;
        AudioSource sfxSource, ambienceSource;
        AudioClip shotSound, pickupSound, impactSound, alarmSound, humSound;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            SetupAudio();
            BuildLevel();
        }

        void SetupAudio()
        {
            sfxSource=gameObject.AddComponent<AudioSource>();sfxSource.spatialBlend=0;sfxSource.volume=.8f;
            ambienceSource=gameObject.AddComponent<AudioSource>();ambienceSource.spatialBlend=0;ambienceSource.volume=.12f;ambienceSource.loop=true;
            shotSound=Tone("Pulse Rifle",.14f,170,58,.7f);
            pickupSound=Tone("Pickup Chime",.24f,580,940,.34f);
            impactSound=Tone("Impact",.11f,110,48,.55f);
            alarmSound=Tone("Reactor Alarm",.65f,390,520,.3f);
            humSound=Tone("Station Hum",1.2f,48,51,.13f);
            ambienceSource.clip=humSound;ambienceSource.Play();
        }
        AudioClip Tone(string clipName,float seconds,float startHz,float endHz,float gain)
        {
            const int rate=22050;int count=Mathf.CeilToInt(rate*seconds);var samples=new float[count];
            for(int i=0;i<count;i++){float t=i/(float)rate;float k=t/seconds;float hz=Mathf.Lerp(startHz,endHz,k);samples[i]=Mathf.Sin(2*Mathf.PI*hz*t)*Mathf.Exp(-4.5f*k)*gain;}
            var clip=AudioClip.Create(clipName,count,1,rate,false);clip.SetData(samples,0);return clip;
        }
        public void PlayShot(){if(sfxSource&&shotSound)sfxSource.PlayOneShot(shotSound);}
        public void PlayImpact(){if(sfxSource&&impactSound)sfxSource.PlayOneShot(impactSound);}
        public void PlayPickup(bool reactor=false){if(sfxSource)sfxSource.PlayOneShot(reactor?alarmSound:pickupSound);}

        void BuildLevel()
        {
            if (world) Destroy(world.gameObject);
            var oldPlayer = GameObject.Find("Marine");
            if (oldPlayer) Destroy(oldPlayer);
            world = new GameObject("Generated Level " + level).transform;
            health = 100;
            var floor = MakeMat(new Color(.12f,.15f,.18f));
            var wall = MakeMat(new Color(.24f,.29f,.31f));
            var glow = MakeMat(new Color(.04f,.65f,.72f), 1.4f);
            Cube("Floor", new Vector3(0,-.5f,24), new Vector3(18,1,60), floor);
            Cube("Ceiling", new Vector3(0,5.5f,24), new Vector3(18,1,60), wall);
            Cube("Left wall", new Vector3(-9,2.5f,24), new Vector3(1,5,60), wall);
            Cube("Right wall", new Vector3(9,2.5f,24), new Vector3(1,5,60), wall);
            for(int i=0;i<5;i++) {
                Cube("Cover",new Vector3(i%2==0?-4:4,.65f,8+i*9),new Vector3(2,1.3f,2),wall);
                Cube("Light",new Vector3(0,4.94f,5+i*10),new Vector3(4,.12f,.22f),glow,false);
            }
            if(level==1) {
                Item("RED KEYCARD",new Vector3(-4,1,10),ItemType.Key,Color.red);
                Enemy(new Vector3(2,0,18)); Enemy(new Vector3(-3,0,32)); Enemy(new Vector3(3,0,42));
            } else if(level==2) {
                Item("TERMINAL 1",new Vector3(-5,1,12),ItemType.Terminal,Color.cyan);
                Item("TERMINAL 2",new Vector3(5,1,26),ItemType.Terminal,Color.cyan);
                Item("TERMINAL 3",new Vector3(-5,1,40),ItemType.Terminal,Color.cyan);
                Enemy(new Vector3(2,0,17)); Enemy(new Vector3(-2,0,31));
            } else {
                Item("REACTOR SHUTDOWN",new Vector3(0,1,44),ItemType.Reactor,Color.red);
                Enemy(new Vector3(-4,0,18)); Enemy(new Vector3(4,0,23)); Enemy(new Vector3(0,0,32),true);
                Cube("Reactor",new Vector3(0,2.5f,43),new Vector3(3,5,3),MakeMat(Color.red,1.5f),false);
            }
            var exit = GameObject.CreatePrimitive(PrimitiveType.Cube);
            exit.name="EXIT"; exit.transform.SetParent(world); exit.transform.position=new Vector3(0,2,54); exit.transform.localScale=new Vector3(4,4,.6f);
            exit.GetComponent<Renderer>().material=glow;
            exit.AddComponent<BoxCollider>().isTrigger=true;
            var exitBody=exit.AddComponent<Rigidbody>();exitBody.isKinematic=true;exitBody.useGravity=false;
            exit.AddComponent<CampaignExit>();
            var lightObj=new GameObject("Emergency Light"); lightObj.transform.SetParent(world); lightObj.transform.position=new Vector3(0,4,12);
            var lamp=lightObj.AddComponent<Light>();lamp.type=LightType.Point;lamp.range=40;lamp.intensity=2.5f;lamp.color=new Color(.4f,.75f,.85f);
            RenderSettings.ambientLight=new Color(.18f,.2f,.24f);
            var p=new GameObject("Marine");player=p.transform;p.transform.position=new Vector3(0,0,2);
            var cc=p.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.38f;cc.center=new Vector3(0,.9f,0);
            var cameraObj=new GameObject("Main Camera");cameraObj.tag="MainCamera";cameraObj.transform.SetParent(p.transform);cameraObj.transform.localPosition=new Vector3(0,1.62f,0);
            view=cameraObj.AddComponent<Camera>();view.fieldOfView=86;cameraObj.AddComponent<AudioListener>();
            p.AddComponent<MarineMotor>();
            Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
        }

        Material MakeMat(Color color,float emission=0)
        {
            var template=Resources.Load<Material>("AbyssalRuntime");
            Material m;
            if(template!=null)m=new Material(template);
            else
            {
                var shader=Shader.Find("Universal Render Pipeline/Lit");
                if(shader==null)shader=Shader.Find("Standard");
                if(shader==null)shader=Shader.Find("Unlit/Color");
                if(shader==null){Debug.LogError("Abyssal Protocol: no compatible material shader found.");return null;}
                m=new Material(shader);
            }
            m.color=color;
            if(emission>0){m.EnableKeyword("_EMISSION");if(m.HasProperty("_EmissionColor"))m.SetColor("_EmissionColor",color*emission);}
            return m;
        }
        void Cube(string n,Vector3 p,Vector3 s,Material m,bool solid=true){var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=n;o.transform.SetParent(world);o.transform.position=p;o.transform.localScale=s;if(m!=null)o.GetComponent<Renderer>().sharedMaterial=m;if(!solid)Destroy(o.GetComponent<Collider>());}
        void Item(string label,Vector3 p,ItemType type,Color color){var o=GameObject.CreatePrimitive(type==ItemType.Terminal?PrimitiveType.Cube:PrimitiveType.Sphere);o.name=label;o.transform.SetParent(world);o.transform.position=p;o.transform.localScale=type==ItemType.Terminal?new Vector3(.8f,1.5f,.4f):Vector3.one*.65f;var material=MakeMat(color,1.2f);if(material!=null)o.GetComponent<Renderer>().material=material;o.GetComponent<Collider>().isTrigger=true;o.AddComponent<CampaignItem>().type=type;}
        void Enemy(Vector3 p,bool boss=false){var o=new GameObject(boss?"THE WARDEN":"Infected");o.transform.SetParent(world);o.transform.position=p;var c=o.AddComponent<CapsuleCollider>();c.height=boss?2.6f:1.8f;c.radius=boss?.65f:.42f;c.center=Vector3.up*c.height*.5f;var body=GameObject.CreatePrimitive(PrimitiveType.Capsule);body.transform.SetParent(o.transform,false);body.transform.localPosition=Vector3.up*c.height*.5f;body.transform.localScale=boss?new Vector3(1.3f,1.3f,1.3f):Vector3.one*.85f;Destroy(body.GetComponent<Collider>());var material=MakeMat(boss?new Color(.7f,.03f,.02f):new Color(.18f,.5f,.24f),.35f);if(material!=null)body.GetComponent<Renderer>().material=material;o.AddComponent<CampaignEnemy>().Setup(boss);}

        public void Interact(ItemType type)
        {
            if(type==ItemType.Key){redKey=true;PlayPickup();Notify("TARJETA ROJA ADQUIRIDA");}
            if(type==ItemType.Terminal){terminals++;PlayPickup();Notify("TERMINAL ACTIVADO");Enemy(new Vector3(Random.Range(-4,4),0,Random.Range(24,42)));Enemy(new Vector3(Random.Range(-4,4),0,Random.Range(24,42)));}
            if(type==ItemType.Reactor){foreach(var e in FindObjectsOfType<CampaignEnemy>())if(e.boss){Notify("ELIMINA AL GUARDIÁN");return;}reactorOff=true;escapeTimer=30;PlayPickup(true);Notify("REACTOR INESTABLE — ¡ESCAPA!");}
        }
        public bool ExitReady{get{return level==1?redKey:level==2?terminals>=3:reactorOff;}}
        public void UseExit(){if(!ExitReady){Notify(level==1?"FALTA LA TARJETA ROJA":level==2?"ACTIVA LOS TRES TERMINALES":"EL REACTOR SIGUE ACTIVO");return;}if(level<3){level++;terminals=0;redKey=false;reactorOff=false;BuildLevel();}else Finish(true,"ESCAPASTE DEL COMPLEJO");}
        public void Hurt(int amount){if(ended)return;health=Mathf.Max(0,health-amount);PlayImpact();if(health==0)Finish(false,"LA BASE TE HA CONSUMIDO");}
        public void Killed(bool isBoss){kills++;if(isBoss)Notify("GUARDIÁN ELIMINADO");}
        void Finish(bool win,string msg){ended=true;victory=win;notice=msg+" — Pulsa R para reiniciar";Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        public void Notify(string msg){notice=msg;noticeUntil=Time.time+3;}
        void Update(){if(level==3&&reactorOff&&!ended){escapeTimer-=Time.deltaTime;if(escapeTimer<=0)Finish(false,"EL REACTOR HA DETONADO");}if(ended&&Input.GetKeyDown(KeyCode.R)){ended=false;level=1;ammo=24;reserve=96;kills=0;redKey=false;terminals=0;reactorOff=false;BuildLevel();}}
        void OnGUI(){if(hudStyle==null){hudStyle=new GUIStyle(GUI.skin.label){fontSize=20};hudStyle.normal.textColor=Color.white;centerStyle=new GUIStyle(hudStyle){alignment=TextAnchor.MiddleCenter,fontSize=26};}string objective=level==1?(redKey?"Abre la esclusa":"Encuentra la tarjeta roja"):level==2?$"Activa terminales ({terminals}/3)":reactorOff?$"¡ESCAPA! {Mathf.CeilToInt(escapeTimer)} s":"Derrota al Guardián y apaga el reactor";string title=level==1?"BASE ABANDONADA":level==2?"LABORATORIO INFECTADO":"REACTOR PRINCIPAL";GUI.Label(new Rect(18,15,700,34),title,hudStyle);GUI.Label(new Rect(18,50,700,34),$"VIDA {health}     MUNICIÓN {ammo}/{reserve}     BAJAS {kills}",hudStyle);GUI.Label(new Rect(Screen.width/2-300,15,600,38),objective,centerStyle);GUI.Label(new Rect(Screen.width/2-8,Screen.height/2-18,20,36),"+",centerStyle);if(Time.time<noticeUntil||ended)GUI.Label(new Rect(Screen.width/2-400,Screen.height*.68f,800,70),notice,centerStyle);if(ended)GUI.Label(new Rect(Screen.width/2-280,Screen.height/2-55,560,50),victory?"VICTORIA":"DERROTA",centerStyle);GUI.Label(new Rect(18,Screen.height-36,900,28),"WASD mover  |  Ratón apuntar  |  Clic disparar  |  R recargar  |  E interactuar",hudStyle);}
        public enum ItemType{Key,Terminal,Reactor}
    }

    public class CampaignItem:MonoBehaviour
    {
        public AbyssalManager.ItemType type;
        void Update(){transform.Rotate(0,50*Time.deltaTime,0);}
        void OnTriggerEnter(Collider other){if(other.GetComponent<MarineMotor>()){AbyssalManager.Instance.Interact(type);Destroy(gameObject);}}
    }
    public class CampaignExit:MonoBehaviour
    {
        void OnTriggerEnter(Collider other){if(other.GetComponent<MarineMotor>())AbyssalManager.Instance.UseExit();}
    }
    public class CampaignEnemy:MonoBehaviour
    {
        public bool boss;int hp;float nextAttack;NavMeshAgent agent;
        public void Setup(bool big){boss=big;hp=big?400:100;if(NavMesh.SamplePosition(transform.position,out NavMeshHit hit,2,NavMesh.AllAreas)){agent=gameObject.AddComponent<NavMeshAgent>();transform.position=hit.position;agent.speed=big?3:3.8f;agent.stoppingDistance=1.5f;}}
        public void Hit(int d){hp-=d;if(hp<=0){AbyssalManager.Instance.Killed(boss);Destroy(gameObject);}}
        void Update(){var g=AbyssalManager.Instance;if(!g||g.ended||g.transform==null)return;var p=GameObject.Find("Marine");if(!p)return;Vector3 target=p.transform.position;if(agent&&agent.isOnNavMesh){agent.SetDestination(target);}else{Vector3 d=target-transform.position;d.y=0;if(d.magnitude>1.5f)transform.position+=d.normalized*(boss?2:2.6f)*Time.deltaTime;}transform.LookAt(new Vector3(target.x,transform.position.y,target.z));if(Time.time>nextAttack&&Vector3.Distance(transform.position,target)<2){nextAttack=Time.time+(boss?1:1.4f);g.Hurt(boss?18:10);}}
    }
    public class MarineMotor:MonoBehaviour
    {
        CharacterController controller;Transform cam;float pitch;float shotAt;
        void Start(){controller=GetComponent<CharacterController>();cam=GetComponentInChildren<Camera>().transform;}
        void Update(){var g=AbyssalManager.Instance;if(!g||g.ended)return;if(Cursor.lockState!=CursorLockMode.Locked){if(Input.GetMouseButtonDown(0)){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}return;}transform.Rotate(0,Input.GetAxis("Mouse X")*2.2f,0);pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*2.2f,-85,85);cam.localRotation=Quaternion.Euler(pitch,0,0);Vector3 move=(transform.right*Input.GetAxisRaw("Horizontal")+transform.forward*Input.GetAxisRaw("Vertical")).normalized;controller.Move((move*6+Vector3.down*2)*Time.deltaTime);if(Input.GetKeyDown(KeyCode.E)&&Physics.Raycast(cam.position,cam.forward,out RaycastHit use,4)){var item=use.collider.GetComponent<CampaignItem>();if(item){g.Interact(item.type);Destroy(item.gameObject);}var exit=use.collider.GetComponentInParent<CampaignExit>();if(exit)g.UseExit();}if(Input.GetKeyDown(KeyCode.R)){int n=Mathf.Min(24-g.ammo,g.reserve);g.ammo+=n;g.reserve-=n;}if(Input.GetMouseButton(0)&&Time.time>shotAt)Shoot(g);}
        void Shoot(AbyssalManager g){if(g.ammo<=0)return;g.ammo--;g.PlayShot();shotAt=Time.time+.18f;if(Physics.Raycast(cam.position,cam.forward,out RaycastHit hit,60)){g.PlayImpact();var enemy=hit.collider.GetComponentInParent<CampaignEnemy>();if(enemy)enemy.Hit(enemy.boss?20:50);var fx=new GameObject("Impact");fx.transform.position=hit.point;var ps=fx.AddComponent<ParticleSystem>();var main=ps.main;main.startLifetime=.25f;main.startSpeed=3;main.startSize=.08f;main.startColor=new Color(1,.4f,.08f);main.loop=false;var em=ps.emission;em.rateOverTime=0;em.SetBursts(new[]{new ParticleSystem.Burst(0,10)});ps.Play();Destroy(fx,.7f);}}
    }
}
