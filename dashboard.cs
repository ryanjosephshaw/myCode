@using WBGLibrary
@model OrderSummary
    <style>
        h1 {
            margin-top: 0px;
            font-size: xx-large;
        }
        h3 {
            margin: 0 0 0 0 !important;
        }
    </style>
<div class="row">
    <div class="col-md-12">
        <div class="box box-primary">
            <div class="box-header with-border">
                <h3 class="box-title"><i class="fa fa-line-chart"></i> Totals</h3>
            </div>
            <div class="box-body box-nested provTotals">
                <div class="col-lg-4 col-xs-6">
                    <div class="small-box bg-aqua ">
                        <div class="inner">
                            <h1>Total Orders</h1>
                            <h3>@Model.Orders.ToString("N0")</h3>
                        </div>
                        <div class="icon">
                            <i class="fa fa-tasks"></i>
                        </div>
                        @*<a href="#" class="small-box-footer" onclick="return getJobs();">
                            More Info
                            <i class="fa fa-arrow-circle-right"></i>
                        </a>*@
                    </div>
                </div>
                <div class="col-lg-4 col-xs-6">
                    <div class="small-box bg-green">
                        <div class="inner">
                            <h1>Total Sales</h1>
                            <h3>@Model.SalesDollars.ToString("C")</h3>
                        </div>
                        <div class="icon">
                            <i class="ion ion-social-usd"></i>
                        </div>
                        @*<a href="#" class="small-box-footer">
                            More Info
                            <i class="fa fa-arrow-circle-right"></i>
                        </a>*@
                    </div>
                </div>
                <div class="col-lg-4 col-xs-6">
                    <div class="small-box bg-yellow ">
                        <div class="inner">
                            <h1>Total Quantity</h1>
                            <h3>@Model.TotalQuantity.ToString("N0")</h3>
                        </div>
                        <div class="icon">
                            <i class="ion ion-cube"></i>
                        </div>
                        @*<a href="#" class="small-box-footer">
                            More Info
                            <i class="fa fa-arrow-circle-right"></i>
                        </a>*@
                    </div>
                </div>
                <div class="col-lg-4 col-xs-6">
                    @(Html.Kendo().Chart(Model.GraphObj.GraphDetails)
                        .Name("totalOrdersChart")
                        .Title("Total Orders")
                        .Series(series =>
                        {
                            series
                                .Line(model => model.OrderTotal, categoryExpression: model => model.Date).Color("DodgerBlue")
                                .Aggregate(ChartSeriesAggregate.Sum);
                        })
                        .CategoryAxis(axis => axis
                            .Date()
                            .Labels(labels =>
                            {
                                labels.Rotation(-90);
                                labels.DateFormats(f => f.Days("dd"));
                            })
                            .BaseUnit(ChartAxisBaseUnit.Days)
                        )
                        .Tooltip(t => t
                            .Visible(true)
                        )
                        .Legend(l => l
                            .Visible(false)
                        )
                    )
                </div>
                <div class="col-lg-4 col-xs-6">
                    @(Html.Kendo().Chart(Model.GraphObj.GraphDetails)
                        .Name("totalSalesChart")
                        .Title("Total Sales")
                        .Series(series =>
                        {
                            series
                                .Line(model => model.SalesDollars, categoryExpression: model => model.Date).Color("Green")
                                .Aggregate(ChartSeriesAggregate.Sum);
                        })
                        .CategoryAxis(axis => axis
                            .Date()
                            .Labels(labels =>
                            {
                                labels.Rotation(-90);
                                labels.DateFormats(f => f.Days("dd"));
                            })
                            .BaseUnit(ChartAxisBaseUnit.Days)
                        )
                        .Tooltip(t => t
                            .Visible(true)
                            .Format("C")
                        )
                        .Legend(l => l
                            .Visible(false)
                        )
                    )
                </div>
                <div class="col-lg-4 col-xs-6">
                    @(Html.Kendo().Chart(Model.GraphObj.GraphDetails)
                        .Name("totalQuantityChart")
                        .Title("Total Quantity")
                        .Series(series =>
                        {
                            series
                                .Line(model => model.TotalQuantity, categoryExpression: model => model.Date).Color("Orange")
                                .Aggregate(ChartSeriesAggregate.Sum);
                        })
                        .CategoryAxis(axis => axis
                            .Date()
                            .Labels(labels =>
                            {
                                labels.Rotation(-90);
                                labels.DateFormats(f => f.Days("dd"));
                            })
                            .BaseUnit(ChartAxisBaseUnit.Days)
                        )
                        .Tooltip(t => t
                            .Visible(true)
                        )
                        .Legend(l => l
                            .Visible(false)
                        )
                    )
                </div>
            </div>
        </div>
    </div>
</div>
