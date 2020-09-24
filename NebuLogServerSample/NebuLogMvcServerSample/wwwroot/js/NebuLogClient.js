$(function () {
    var $table = $('#messagesList');
    $table.bootstrapTable('resetView');
    var dataCache = [];

    $table.on('refresh.bs.table', function (e, params) {
        $table.bootstrapTable('removeAll');
        var allData = $table.bootstrapTable('getData', false);
        refreshStats(allData);
    });
    $table.on('search.bs.table', function (e, text) {
        var allData = $table.bootstrapTable('getData', false);
        refreshStats(allData);
    });

    const connection = new signalR
        .HubConnectionBuilder()
        .withUrl("/NebuLogHub")
        .configureLogging(signalR.LogLevel.Information)
        .build();

    connection.start().catch(err => console.error(err.toString()));


    connection.on("OnILogging", (time, project, source, loglevel, message) => {
        $table.bootstrapTable('append', appendata(time, project, source, loglevel, message));
        $table.bootstrapTable('scrollTo', 'bottom');

        var allData = $table.bootstrapTable('getData', false);
        refreshStats(allData);
        //console.log(new Date() + ' : ' + project +' : ' + source + ' : ' + loglevel + '(' + message + ')');
    });
    connection.on("OnNebuLogCustom", (username, loginfo) => {
        $table.bootstrapTable('append', appendLog(username, loginfo));
        $table.bootstrapTable('scrollTo', 'bottom');
    });
    connection.on("OnNebuLogException", (time, project, source, loglevel, exception) => {
        $table.bootstrapTable('append', appendException(time, project, source, loglevel, exception));
        $table.bootstrapTable('scrollTo', 'bottom');

        var allData = $table.bootstrapTable('getData', false);
        refreshStats(allData);
        //console.log(new Date() + ' : ' + project +' : ' + source + ' : ' + loglevel + '(' + message + ')');
    });
    var statsPanel = $('#statsPanel');
    connection.on("OnAddCustomStats", (statId, statTitle, color) => {
        var v_li = document.createElement("li");//生成li
        v_li.innerHTML = "<i class='glyphicon glyphicon-asterisk " + color + "'></i>" + statTitle +
            "<span class='" + color + " pull-right' id='" + statId + "'>?</span >";//添加li中要显示的内容
        statsPanel.append(v_li);
    });
    connection.on("OnLogCustomStats", (statId, message) => {
        $("#" + statId).text(message);
        console.log('OnLogCustomStats' + new Date() + ' : ' + statId + ' : ' + message);
    });

    //Left menu items action 左侧分类筛选器
    $('#left-menu-onTrace').click(function () {
        filterData($table, 'Trace');
    });
    $('#left-menu-onDebug').click(function () {
        filterData($table, 'Debug');
    });
    $('#left-menu-onInformation').click(function () {
        filterData($table, 'Information');
    });
    $('#left-menu-onWarning').click(function () {
        filterData($table, 'Warning');
    });
    $('#left-menu-onError').click(function () {
        filterData($table, 'Error');
    });
    $('#left-menu-onCritical').click(function () {
        filterData($table, 'Critical');
    });
    //Frank: 2019-01-28新增Exception的处理
    $('#left-menu-onException').click(function () {
        filterData($table, 'Exception');
    });
});

function appendata(timeinput, projectinput, sourceinput, loglevelinput, messageinput) {
    var rows = [];

    rows.push({
        Time: timeinput,
        Project: projectinput,
        Sender: sourceinput,
        LogLevel: loglevelinput,
        Message: messageinput
    });
    return rows;
}
function appendException(timeinput, projectinput, sourceinput, loglevelinput, exception) {
    var rows = [];
    rows.push({
        Time: timeinput,
        Project: projectinput,
        Sender: sourceinput,
        LogLevel: loglevelinput,
        Message: exception
    });
    return rows;
}
function appendLog(user, log) {
    var rows = [];
    rows.push({
        Time: log.TimeOfLog,
        Sender: log.Sender,
        Message: log.Message
    });
    return rows;
}



//刷新Stats面板数值
function refreshStats(data) {
    $('#stat-all').text(data.length);
    $('#stat-Trace').text($.grep(data, function (log) { return log.LogLevel === 'Trace'; }).length);
    $('#stat-Debug').text($.grep(data, function (log) { return log.LogLevel === 'Debug'; }).length);
    $('#stat-Information').text($.grep(data, function (log) { return log.LogLevel === 'Information'; }).length);
    $('#stat-Warning').text($.grep(data, function (log) { return log.LogLevel === 'Warning'; }).length);
    $('#stat-Error').text($.grep(data, function (log) { return log.LogLevel === 'Error'; }).length);
    $('#stat-Critical').text($.grep(data, function (log) { return log.LogLevel === 'Critical'; }).length);
    //Frank: 2019-01-28新增Exception的处理
    $('#stat-Exception').text($.grep(data, function (log) { return log.LogLevel === 'Exception'; }).length);
}

/*Deprecated: filterData0函数采用了datatable数组遍历的方式，效率太低。
function filterData0(datatable, loglevel) {
    $.each(
        datatable.bootstrapTable('getData', false),
        function (indexofrow, element) {
            if (element.LogLevel !== loglevel) {
                datatable.bootstrapTable('hideRow', { index: indexofrow });
            } else {
                datatable.bootstrapTable('showRow', { index: indexofrow });
            }
        });
    $('#messagesList').bootstrapTable('scrollTo', 'bottom');
}*/
//将指定loglevel以外的数据过滤；
function filterData(datatable, loglevel) {
    datatable.bootstrapTable('resetSearch', loglevel);
    datatable.bootstrapTable('scrollTo', 'bottom');
}


//根据data内容修改TableRow的格式
function rowStyle(row, index) {
    if (row.LogLevel === "Trace") { return {}; }
    if (row.LogLevel === "Debug") { return {}; }
    if (row.LogLevel === "Information") { return row.LogLevel.indexOf('Microsoft') ? {} : { classes: "info" }; }
    if (row.LogLevel === "Warning") { return { classes: "warning" }; }
    if (row.LogLevel === "Error") { return { classes: "danger" }; }
    if (row.LogLevel === "Critical") { return { classes: "danger" }; }
    //Frank: 2019-01-28新增Exception的处理
    if (row.LogLevel === "Exception") { return { classes: "danger" }; }
    return {};
}
function cellStyle(value, row, index, field) {
    return {
        classes: 'cell'
    };
}

//Detail view 详情视图
function detailFormatter(index, row) {
    var html = [];
    $.each(row, function (key, value) {
        if (value.indexOf("<xml>") !== -1) {
            var start = value.indexOf("<xml>");
            var end = value.indexOf("</xml>");
            var xml = value.substring(start, end);
            var formatted = formatXml(xml);
                var count = 0;
            var linecount = function (sourcetext) {
                for (var i = 0, I = sourcetext.length; i < I; i++) {
                    if (sourcetext.substring(i, 4) === '\n')
                        count++;
                }
                return count;
            };
            value = "<textarea rows='" + count + "' style='width:90%'>" + formatted + "</textarea>";
        }
        html.push('<p><b>' + key + ':</b> ' + value + '</p>');
    });
    return html.join('');
}
//对表格数据的格式进行格式处理
function timeFormatter(value, row) {
    //鸣谢：DateFormat代码基于eguid提供的参考修改而来。
    //https://blog.csdn.net/eguid_1/article/details/53736579
    function dateSimplifyFormatter(str, d) {
        //填充0  
        function fillZero(value) {
            if (value.toString().length < 2) {
                return "0" + value;
            }
            return value;
        }
        //判空  
        function checkNull(value) {
            if (!value || value === null || typeof (value) === "undefined" || value === "") {
                return true;
            }
            return false;
        }


        if (checkNull(str)) {  //如果格式化字符为空，返回空字符  
            return "";
        }
        if (checkNull(d)) {  //如果日期为空，自动获取当前日期  
            d = new Date();
        } else if (d.constructor !== Date) {//如果参数不是一个日期对象，就认为是一个标准Long值日期
            d = new Date(d);
        }
        return str
            .replace("yyyy/", '')
            .replace("MM/", '')
            .replace("dd", '')
            .replace("HH", fillZero(d.getHours()))
            .replace("mm", fillZero(d.getMinutes()))
            .replace("ss", fillZero(d.getSeconds()))
            .replace("fff", fillZero(d.getMilliseconds()));
    }

    return dateSimplifyFormatter('yyyy/MM/dd HH:mm:ss.fff', value);
}
function senderFormatter(value, row) {
    return value.substring(value.lastIndexOf(".") + 1, value.length);
}
function loglevelFormatter(value, row) {
    switch (row.LogLevel) {
        case "Trace": return "<span class='label label-success'>Trace</span>";
        case "Debug": return "<span class='label label-default'>Debug</span>";
        case "Information": return "<span class='label label-info'>Info</span>";
        case "Warning": return "<span class='label label-warning'>Warning</span>";
        case "Error": return "<span class='label label-danger'>Error</span>";
        case "Critical": return "<span class='label label-danger'>Critical</span>";
        //Frank: 2019-01-28新增Exception的处理
        case "Exception": return "<span class='label label-danger'>Exception</span>";
    }
    return value;
}

function messageFormatter(value, row) {
    //如果收到的message包含了XML格式信息，浏览器默认会渲染XML，因此需要对消息先进行一些处理。
    if (value.indexOf("<xml>") !== -1) {
        var start = value.indexOf("<xml>");
        var end = value.indexOf("</xml>");
        var xml = value.substring(start, end);
        value = value.replace(xml, '');
        value = value + '<button class="btn btn-success btn-xs"> XML data </button>';
    }
    var brPos = value.indexOf("<br>");
    if (brPos > 79) brPos = 79;
    if (brPos <= 0) brPos = 79;
    return value.substring(0, brPos) + (value.length > 80 ? '...' : '');
}
function projectFormatter(value, row) {
    return value.substring(value.lastIndexOf(".") + 1, value.length);
}

//作者：yk10010
//来源：CSDN
//原文：https://blog.csdn.net/yk10010/article/details/81739393 
//版权声明：本文为博主原创文章，转载请附上博文链接！
function formatXml(text) {
    //去掉多余的空格 
    text = '\n' + text.replace(/(<\w+)(\s.*?>)/g, function ($0, name, props) { return name + ' ' + props.replace(/\s+(\w+=)/g, " $1"); }).replace(/>\s*?</g, ">\n<"); 
    //把注释编码 
    text = text.replace(/\n/g, '\r').replace(/<!--(.+?)-->/g, function ($0, text) {
        var ret = '<!--' + escape(text) + '-->';
        //alert(ret); 
        return ret;
    }).replace(/\r/g, '\n'); 
        //调整格式 
    var rgx = /\n(<(([^\?]).+?)(?:\s|\s*?>|\s*?(\/)>)(?:.*?(?:(?:(\/)>)|(?:<(\/)\2>)))?)/mg;
    var nodeStack = [];
    var output = text.replace(rgx, function ($0, all, name, isBegin, isCloseFull1, isCloseFull2, isFull1, isFull2) {
        var isClosed = (isCloseFull1 === '/') || (isCloseFull2 === '/') || (isFull1 === '/') || (isFull2 === '/');
        //alert([all,isClosed].join('=')); 
        var prefix = '';
        if (isBegin === '!') { prefix = getPrefix(nodeStack.length); }
        else {
            if (isBegin !== '/') {
                prefix = getPrefix(nodeStack.length);
                if (!isClosed) { nodeStack.push(name); }
            }
            else { nodeStack.pop(); prefix = getPrefix(nodeStack.length); }
        }
        var ret = '\n' + prefix + all;
        return ret;
    });
    var prefixSpace = -1;
    var outputText = output.substring(1);
    //alert(outputText); 
    //把注释还原并解码，调格式 
    outputText = outputText.replace(/\n/g, '\r').replace(/(\s*)<!--(.+?)-->/g, function ($0, prefix, text) {
        //alert(['[',prefix,']=',prefix.length].join('')); 
        if (prefix.charAt(0) === '\r')
            prefix = prefix.substring(1);
        text = unescape(text).replace(/\r/g, '\n');
        var ret = '\n' + prefix + '<!--' + text.replace(/^\s*/mg, prefix) + '-->';
        //alert(ret); 
        return ret;
    });
    return outputText.replace(/\s+$/g, '').replace(/\r/g, '\r\n');
}
function getPrefix(prefixIndex) {
    var span = '    ';
    var output = [];
    for (var i = 0; i < prefixIndex; ++i) { output.push(span); }
    return output.join('');
}
//---------------------
