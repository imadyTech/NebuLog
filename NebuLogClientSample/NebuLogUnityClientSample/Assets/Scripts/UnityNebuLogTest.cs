using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class UnityNebuLogTest : MonoBehaviour
{
    public int counter = 0;
    public Text nullReferenceExceptionObject;

    void OnEnable()
    {
        App.logger.AddCustomStats("TuiPairsCount", "Count of TuiPairs", "blue", string.Empty);
        App.logger.AddCustomStats("TuiIdsCount", "Count of TuiIds", "blue", "");
        App.logger.AddCustomStats("TouchesCount", "Count of touch points", "blue", "NA");
        App.logger.AddCustomStats("Coincidence", "TuiMatchResult Coincidence", "blue", "???");

        App.logger.LogCustomStats("TuiPairsCount", "blue");
        App.logger.LogCustomStats("TuiIdsCount", "green");
        App.logger.LogCustomStats("TouchesCount", "Red");
        App.logger.LogCustomStats("Coincidence", "xxx");
    }

    private void Start()
    {
        //演示一下真实的报错事件
        nullReferenceExceptionObject.text = "看看能不能加上文字";
        
    }

    void Update()
    {
        if (counter > 500) return;

        //demo -- 连续输出日志
        if (counter % 10 == 0) Debug.Log(counter.ToString()); //hubconnection重连等待100，30可以，
        counter++;
        //人为搅个错误
        if (counter==300) throw new Exception("测试一把人为报错");

    }

}
