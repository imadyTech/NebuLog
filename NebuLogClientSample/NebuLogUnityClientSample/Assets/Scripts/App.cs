using System;
using UnityEngine;
using UnityEngine.UI;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using imady.NebuLog.Loggers;
using imady.NebuLog.DataModel;

public class App : MonoBehaviour
{
    //public static IUnityNebulog logger;
    public static imady.NebuLog.Loggers.INebuLogger logger;
    public GameObject UnityNebuLogTestObject;
    public Text hubStatusText;

    public async void Awake()
    {
        //logger = new UnityNebulogger();
        var option = new NebuLogOption()
        {
            NebuLogHubUrl = "http://localhost:5999/NebuLogHub",
            ProjectName = Application.productName,
            LogLevel = LogLevel.Trace
        };
        logger = new imady.NebuLog.Loggers.NebuLogger(option, SceneManager.GetActiveScene().name);


        // 注册Debug.Log响应委托以获取日志消息
#if UNITY_4
            Application.RegisterLogCallback(logger.HandleUnityLogs);
#else
        Application.logMessageReceived += logger.HandleUnityLogs;
#endif

        //这条log基本上是收不到的。You are unlikely to receive this log as the Nebulogger is still in connection.
        Debug.Log("App Start 1.");

        //注册到HubConnrvyion连接完成事件，进行业务模块加载
        logger.NebulogConnected += (sender, args) =>
        {
            //==================================================================
            //等待UnityNebuLogger初始化完成（HubConnection连接后）才加载业务逻辑
            //否则可能引起HubConnection未连接就被调用，而导致进程锁死
            UnityNebuLogTestObject.AddComponent<UnityNebuLogTest>();
            //==================================================================
            hubStatusText.text += "\nSignalR HubConnection连接完成。";
            Debug.Log("App initiation completed.");

            //send some test messages async...
            Action test = async () =>
             {
                 //经测试发现800+以上delay比较好的防止了HubConnection连接错误；可能与计算机速度有关系。
                 await Task.Delay(100);
                 Debug.Log("App Start 2.");
             };

            test.Invoke();
        };
    }



    public void OnEnable()
    {


    }
    // Start is called before the first frame update
    void Start()
    {
        //这段代码防止在Hubconnection完成连接之前就发送信息导致错误。
    }

    // Update is called once per frame
    void Update()
    {
        // exit
        if (Input.GetKeyDown(KeyCode.Home) || Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
            Debug.Log("App escaped.");
        }
    }



}
