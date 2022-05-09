using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using UnityNebulog;

public class App : MonoBehaviour
{
    public static IUnityNebulog logger;
    public GameObject UnityNebuLogTestObject;
    public Text hubStatusText;

    public void Awake()
    {
        logger = new UnityNebulogger();

        // 注册Debug.Log响应委托以获取日志消息
        #if UNITY_4
            Application.RegisterLogCallback(HandleUnityLogs);
        #else
            Application.logMessageReceived += logger.HandleUnityLogs;
        #endif

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
        };

        //！！！此时不能立即向NebuLog输出信息，因为HubConnection尚未完成连接。
    }



    public void OnEnable()
    {

        
    }
    // Start is called before the first frame update
    void Start()
    {
        //这段代码防止在Hubconnection完成连接之前就发送信息导致错误。
        Action test = async () =>
         {
             //经测试发现800+以上delay比较好的防止了HubConnection连接错误；可能与计算机速度有关系。
             await Task.Delay(800);
             Debug.Log("App Start 2.");
         };

        //这条log基本上是收不到的。
        test.Invoke();
             Debug.Log("App Start 1.");
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
