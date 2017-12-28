<style>
    #providenceDashboard .input-group {
        display: table-cell !important;
    }
    #providenceDashboard .k-autocomplete, .k-colorpicker, .k-combobox, .k-datepicker, .k-datetimepicker, .k-dropdown, .k-numerictextbox, .k-selectbox, .k-textbox, .k-timepicker, .k-toolbar .k-split-button {
        width: 320px !important;
    }
</style>
<div class="content" data-tab="w20-EPMSDashboard-Customer" id="providenceDashboard">
    <section class="content-header">
        <h1>
            EPMS Customer Dashboard
        </h1>
    </section>
    <section class="content">
        <div class="row">
            <div class="col-md-12">
                <div class="box box-primary">
                    <div class="box-body box-nested">
                        <div class="input-group">
                            <div id="reportrange" class="pull-right" style="background: #fff; cursor: pointer; width: 320px; padding: 3px 10px; border: 1px solid #ccc; border-bottom-right-radius: 4px !important; border-bottom-left-radius: 4px !important; border-top-left-radius: 4px !important; border-top-right-radius: 4px !important; font-size: 16px !important; height: 35px !important">
                                <i class="glyphicon glyphicon-calendar fa fa-calendar"></i>&nbsp;
                                <span></span> <b class="caret"></b>
                            </div>
                        </div>
                        @if (User.IsInAnyRole("All Locations"))
                        {
                            <div class="input-group">
                                <div id="plantSelector" class="pull-right" style="font-size: 16px !important; height: 35px !important; padding: 0 5px 5px 5px">
                                    @(Html.Kendo().DropDownList()
                                    .Name("PlantSelection")
                                    .DataTextField("Text")
                                    .DataValueField("Value")
                                    .BindTo(new List<SelectListItem>()
                                    {
                                        new SelectListItem()
                                        {
                                            Text = "All Locations",
                                            Value = ""
                                        },
                                        new SelectListItem()
                                        {
                                            Text = "Chino",
                                            Value = "07",
                                        },
                                        new SelectListItem()
                                        {
                                            Text = "East",
                                            Value = "01"
                                        },
                                        new SelectListItem()
                                        {
                                            Text = "Kent",
                                            Value = "02"
                                        },
                                        new SelectListItem()
                                        {
                                            Text = "Warehouse",
                                            Value = "01w"
                                        },
                                        new SelectListItem()
                                        {
                                            Text = "West",
                                            Value = "03"
                                        }
                                    })
                                    .Value("All Locations")
                                    .Events(e =>
                                        e.Change("GetDash")
                                    )
                                    )
                                </div>
                            </div>
                        }
                        <div class="input-group">
                            <div id="customerSelector" class="pull-right" style="font-size: 16px !important; height: 35px !important">
                                @(Html.Kendo().DropDownList()
                                .Name("EPMSCustomers")
                                .DataTextField("Name")
                                .DataValueField("Account")
                                .Filter("contains")
                                .DataSource(source =>
                                {
                                    source.Read(read =>
                                    {
                                        read.Action("GetEPMSCustomers", "EPMSDashboard");
                                    });
                                })
                                .SelectedIndex(0)
                                .Events(e =>
                                    e.Change("CustomerChange")
                                        )
                                    )
                            </div>
                        </div>
                        <div id="tsgTools"></div>
                    </div>
                </div>
            </div>
        </div>        
        <div id="dashDetails">
        </div>
        <div id="customStats"></div>
        <div id="tsgBC"></div>
        <div id="tsgEnv"></div>
        @if (User.IsInAnyRole("All Locations"))
        {
            <div id="plantComp"></div>
        }
        <div id="details">
        </div>

    </section>
</div>

<script>
    var startDate;
    var endDate;
    var numDays;
    var cust;

    $(document).ready(function () {
        var start = moment().startOf('month');
        var end = moment();
        $("#tsgTools").kendoToolBar({
            items: [
                    {
                        type: "buttonGroup",
                        buttons: [
                            { text: "All", id: "All", togglable: true, selected: true, group: "radio", toggle: tsgbuttonToggleHandler, attributes: { "style": "width:320px; margin-left:-2px; border-radius: 4px;border-left-color:#aeaeae;"} },
                            { text: "Business Cards", id: "BC", togglable: true, group: "radio", toggle: tsgbuttonToggleHandler, attributes: { "style": "width:320px; margin-left:5px; border-radius: 4px;border-left-color:#aeaeae;" } },
                            { text: "Envelopes", id: "Env", togglable: true, group: "radio", toggle: tsgbuttonToggleHandler, attributes: { "style": "width:320px; margin-left:5px; border-radius: 4px;border-left-color:#aeaeae;" } },
                            { text: "Letterhead", id: "LH", togglable: true, group: "radio", toggle: tsgbuttonToggleHandler, attributes: { "style": "width:320px; margin-left:5px; border-radius: 4px;border-left-color:#aeaeae;" } }
                        ]
                    }
            ]
        });
        $('#reportrange').daterangepicker({
            startDate: start,
            endDate: end,
            opens: "center",
            ranges: {
                'This Month': [moment().startOf('month'), moment()],
                'Last Month': [moment().subtract(1, 'month').startOf('month'), moment().subtract(1, 'month').endOf('month')],
                'Last 6 Months': [moment().subtract(6, 'month').startOf('month'), moment().subtract(1, 'month').endOf('month')],
                'Last Year': [moment().subtract(13, 'month').startOf('month'), moment().subtract(1, 'month').endOf('month')]
            }
        }, cb);
        function cb(start, end) {
            startDate = start;
            endDate = end;
            numDays = end.diff(start, 'days');
            $('#reportrange span').html(start.format('MMMM D, YYYY') + ' - ' + end.format('MMMM D, YYYY'));
            var plant = $("#PlantSelection").val();
            cust = $("#EPMSCustomers").val();
            if (cust == undefined) {
                cust = '';
            }
            if (plant == "") {
                GetPlantComparison();
            } else {
                $("#plantComp").hide();
            }
            if (cust == "196362") {
                $("#tsgTools").show();
                GetCustomerDetail();
                GetTsgDetail();
                var toolbar = $("#tsgTools").data("kendoToolBar");
                var selected = toolbar.getSelectedFromGroup("radio");
                var itemType = selected.attr("id");
                if (itemType == "BC") {
                    $("#tsgBC").show();
                    GetTsgBCDetail();
                } else {
                    $("#tsgBC").hide();
                }
                if (itemType == "Env") {
                    $("#tsgEnv").show();
                    GetTsgEnvDetail();
                } else {
                    $("#tsgEnv").hide();
                }
            } else {
                GetCustomerDetail();
                $("#tsgTools").hide();
            }
        };
        cb(start, end);
    });
    function GetDash() {
        var plant = $("#PlantSelection").val();
        cust = $("#EPMSCustomers").val();
        if (plant == "") {
            GetPlantComparison();
        } else {
            $("#plantComp").hide();
        }
        if (cust == "196362") {
            $("#tsgTools").show();
            GetCustomerDetail();
            GetTsgDetail();
            var toolbar = $("#tsgTools").data("kendoToolBar");
            var selected = toolbar.getSelectedFromGroup("radio");
            var itemType = selected.attr("id");
            if (itemType == "BC") {
                $("#tsgBC").show();
                GetTsgBCDetail();
            } else {
                $("#tsgBC").hide();
            }
            if (itemType == "Env") {
                $("#tsgEnv").show();
                GetTsgEnvDetail();
            } else {
                $("#tsgEnv").hide();
            }
        } else {
            $("#tsgTools").hide();
            GetCustomerDetail();
        }
    };

    function CustomerChange() {
        var plant = $("#PlantSelection").val();
        cust = $("#EPMSCustomers").val();
        if (plant == "") {
            GetPlantComparison();
        } else {
            $("#plantComp").hide();
        }
        if (cust == "196362") {
            $("#tsgTools").show();
            $("#customStats").show();            
            GetCustomerDetail();
            GetTsgDetail();
            var toolbar = $("#tsgTools").data("kendoToolBar");
            var selected = toolbar.getSelectedFromGroup("radio");
            var itemType = selected.attr("id");
            if (itemType == "BC") {
                $("#tsgBC").show();
                GetTsgBCDetail();
            } else {
                $("#tsgBC").hide();
            }
            if (itemType == "Env") {
                $("#tsgEnv").show();
                GetTsgEnvDetail();
            } else {
                $("#tsgEnv").hide();
            }
        } else {
            $("#tsgTools").hide();
            $("#customStats").hide();
            $("#tsgBC").hide();
            $("#tsgEnv").hide();
            GetCustomerDetail();
        }
    };

    function refreshChartsDays() {
        var ordersChart = $("#totalOrdersChart").data("kendoChart");
        var salesChart = $("#totalSalesChart").data("kendoChart");
        var quantityChart = $("#totalQuantityChart").data("kendoChart");
        ordersSeries = ordersChart.options.series;
        salesSeries = salesChart.options.series;
        quantitySeries = quantityChart.options.series;
        ordersCategoryAxis = ordersChart.options.categoryAxis;
        salesCategoryAxis = salesChart.options.categoryAxis;
        quantityCategoryAxis = quantityChart.options.categoryAxis;
        ordersCategoryAxis.baseUnit = "days";
        salesCategoryAxis.baseUnit = "days";
        quantityCategoryAxis.baseUnit = "days";
        ordersChart.refresh();
        salesChart.refresh();
        quantityChart.refresh();
    }

    function refreshChartsWeeks() {
        var ordersChart = $("#totalOrdersChart").data("kendoChart");
        var salesChart = $("#totalSalesChart").data("kendoChart");
        var quantityChart = $("#totalQuantityChart").data("kendoChart");
        ordersSeries = ordersChart.options.series;
        salesSeries = salesChart.options.series;
        quantitySeries = quantityChart.options.series;
        ordersCategoryAxis = ordersChart.options.categoryAxis;
        salesCategoryAxis = salesChart.options.categoryAxis;
        quantityCategoryAxis = quantityChart.options.categoryAxis;
        ordersCategoryAxis.baseUnit = "weeks";
        salesCategoryAxis.baseUnit = "weeks";
        quantityCategoryAxis.baseUnit = "weeks";
        ordersChart.refresh();
        salesChart.refresh();
        quantityChart.refresh();
    }

    function refreshChartsMonths() {
        var ordersChart = $("#totalOrdersChart").data("kendoChart");
        var salesChart = $("#totalSalesChart").data("kendoChart");
        var quantityChart = $("#totalQuantityChart").data("kendoChart");
        ordersSeries = ordersChart.options.series;
        salesSeries = salesChart.options.series;
        quantitySeries = quantityChart.options.series;
        ordersCategoryAxis = ordersChart.options.categoryAxis;
        salesCategoryAxis = salesChart.options.categoryAxis;
        quantityCategoryAxis = quantityChart.options.categoryAxis;
        ordersCategoryAxis.baseUnit = "months";
        salesCategoryAxis.baseUnit = "months";
        quantityCategoryAxis.baseUnit = "months";
        ordersChart.refresh();
        salesChart.refresh();
        quantityChart.refresh();
    }

    function tsgbuttonToggleHandler(e) {
        var plant = $("#PlantSelection").val();
        GetCustomerDetail();
        GetTsgDetail();
        if (plant == "") {
            GetPlantComparison();
        } else {
            $("#plantComp").hide();
        }
        var toolbar = $("#tsgTools").data("kendoToolBar");
        var selected = toolbar.getSelectedFromGroup("radio");
        var itemType = selected.attr("id");
        if (itemType == "BC") {
            $("#tsgBC").show();
            GetTsgBCDetail();
        } else {
            $("#tsgBC").hide();
        }
        if (itemType == "Env") {
            $("#tsgEnv").show();
            GetTsgEnvDetail();
        } else {
            $("#tsgEnv").hide();
        }
    }
    function GetPlantComparison() {
        $.ajax({
            type: "POST",
            url: '@Url.Action("PlantComparison", "EPMSDashboard")',
            traditional: true,
            data: JSON.stringify(BuildArguments()),
            dataType: 'html',
            contentType: "application/json; charset=utf-8",
            processData: false
        }).success(function (e) {
            $("#plantComp").show();
            $("#plantComp").html(e);
        });
    }
    function GetCustomerDetail() {
        $.ajax({
            type: "POST",
            url: '@Url.Action("CustomerDetail", "EPMSDashboard")',
            traditional: true,
            data: JSON.stringify(BuildArguments()),
            dataType: 'html',
            contentType: "application/json; charset=utf-8",
            processData: false
        }).success(function (e) {
            $("#dashDetails").html(e);
            if (numDays <= 13) {
                refreshChartsDays();
            }
            if (numDays > 13) {
                refreshChartsWeeks();
            }
            if (numDays > 167) {
                refreshChartsMonths();
            }
        });
    }
    function BuildArguments() {
        var plant = $("#PlantSelection").val();
        var itemType;
        if (plant == undefined)
            plant = "";
        cust = $("#EPMSCustomers").val();
        if (cust == undefined)
            cust = "";
        if (cust == "196362") {
            var toolbar = $("#tsgTools").data("kendoToolBar");
            var selected = toolbar.getSelectedFromGroup("radio");
            itemType = selected.attr("id");
        } else {
            itemType = null;
        }
        return { startDate: startDate.format('MM/DD/YYYY'), endDate: endDate.format('MM/DD/YYYY'), plant: plant, cust: cust, type: itemType };
    }
    function GetTsgDetail() {
        $.ajax({
            type: "POST",
            url: '@Url.Action("TSGDetail", "EPMSDashboard")',
            traditional: true,
            data: JSON.stringify(BuildArguments()),
            dataType: 'html',
            contentType: "application/json; charset=utf-8",
            processData: false
        }).success(function (g) {
            $("#customStats").html(g);
        });
    }
    function GetTsgBCDetail() {
        $.ajax({
            type: "POST",
            url: '@Url.Action("TSGBCDetail", "EPMSDashboard")',
            traditional: true,
            data: JSON.stringify(BuildArguments()),
            dataType: 'html',
            contentType: "application/json; charset=utf-8",
            processData: false
        }).success(function (g) {
            $("#tsgBC").html(g);
        });
    }
    function GetTsgEnvDetail() {
        $.ajax({
            type: "POST",
            url: '@Url.Action("TSGEnvDetail", "EPMSDashboard")',
            traditional: true,
            data: JSON.stringify(BuildArguments()),
            dataType: 'html',
            contentType: "application/json; charset=utf-8",
            processData: false
        }).success(function (g) {
            $("#tsgEnv").html(g);
        });
    }
</script>