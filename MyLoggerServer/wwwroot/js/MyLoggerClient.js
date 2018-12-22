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
        .withUrl("/MyLoggerHub")
        .configureLogging(signalR.LogLevel.Information)
        .build();

    connection.start().catch(err => console.error(err.toString()));


    connection.on("SendMessage", (time, source, loglevel, message) => {
        $table.bootstrapTable('append', appendata(time, source, loglevel, message));
        $table.bootstrapTable('scrollTo', 'bottom');

        var allData = $table.bootstrapTable('getData', false);
        refreshStats(allData);
        console.log(new Date() + ' : ' + source + ' : ' + loglevel + '(' + message + ')');
    });
    connection.on("OnLogging", (username, loginfo) => {
        $table.bootstrapTable('append', appendLog(username, loginfo));
        $table.bootstrapTable('scrollTo', 'bottom');
    });

    //Left menu items action
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
});

function appendata(timeinput, sourceinput, loglevelinput, messageinput) {
    var rows = [];

    rows.push({
        Time: timeinput,
        Sender: sourceinput,
        LogLevel: loglevelinput,
        Message: messageinput
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
        html.push('<p><b>' + key + ':</b> ' + value + '</p>')
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
    }
    return value;
}
function messageFormatter(value, row) {
    return value.substring(0, 99) + (value.length > 100 ? '...' : '');
}

