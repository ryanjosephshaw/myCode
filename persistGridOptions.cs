@model List<WBGLibrary.EPMSDashboard>
<style>
        #EPMSDashboard .k-grid-content {
            height: 100% !important;
        }

        #EPMSDashboard .k-widget .k-grid .k-reorderable {
            height: 100% !important;
        }
        .k-header-column-menu.k-state-active {
            background-color: #3c8dbc !important;
        }
       
        .k-tab-on-top.k-state-active {
            z-index: 1000;
            border-top-right-radius: 22px;
            border-top-left-radius: 22px;
            border: 1px solid rgb(221,221,221);
            bottom:1px;
        }
        .k-item.k-state-focused {
            box-shadow: none;
        }
        #tabstrip {
            width: 500px;
        }
        .tabStrip {
            min-width: 110px;
            max-width:150px;
            text-align:center;
        }
        .k-item.k-state-default.k-state-hover {
            border-top-right-radius: 22px;
            border-top-left-radius: 22px;
            border: 1px solid rgb(221,221,221);
            bottom:1px;
        }
        .k-tabstrip-top.k-tabstrip-items.k-state-active{
            border-bottom-color: #ccc !important;
        }
        .k-item.k-state-default {
            border-top-right-radius: 22px;
            border-top-left-radius: 22px;
            border: 1px solid rgb(221,221,221);
            bottom:1px;
        }
        .k-tabstrip-items .k-item.k-state-active {
         background: #428bca;
        }
        .k-item.k-state-default.k-tab-on-top.k-state-active {
            background-color: #428bca;
        }
        .k-item.k-state-default.k-state-active>span.k-link {
            color: #fff;
        }
        .k-tabstrip:focus {
            -webkit-box-shadow: none;
            box-shadow: none;
        }

        .k-tabstrip-wrapper {
            float: right;
            height: 29px;
        }

        .k-tabstrip-items.k-item.k-state-active {
            background-color: #ebebeb !important;
        }

        .k-tabstrip-top.k-tabstrip-items.k-state-active {
            border-bottom: none;
        }

        span.k-loading.k-complete {
            display: none;
        }

        .k-tabstrip-items.k-loading {
            border-top: none;
        }

        li.k-item.k-state-default {
            z-index: 1000;
        }

        #saveandclose {
            border-color: #3f51b5;
            background-color: #3f51b5;
            color: #fff;
            border-radius: 2px;
            margin: 0;
            padding: 2px 15px 3px;
            font-family: inherit;
            line-height: 1.72em;
            text-align: center;
            cursor: pointer;
            text-decoration: none;
            border-style: solid;
            border-width: 1px;
            -webkit-appearance: none;
        }

        #epmsairportContainer {
            display: none;
            padding: 10px;
            width: 100%;
            height: 100% !important;
            overflow: auto;
        }
        /*#EPMSDashboard .k-active-filter, .k-state-active, .k-state-active:hover, .k-tabstrip .k-state-active {
        background-color: #3c8dbc !important;
    }*/
        .yellow {
            background-color: yellow;
        }

        .pink {
            background-color: pink;
        }

        .green {
            background-color: lawngreen;
        }

        .orange {
            background-color: orange;
        }
</style>
<script>
    var gridViewName = "";
    $(document).ready(function(){
        $("#showFilterButton").hide();

        $("#showFilter").click(function () {
            $("#filterButton").hide();
            $("#showFilterButton").show();
        });

        $("#hideFilter").click(function () {
            $("#filterButton").show();
            $("#showFilterButton").hide();
        });
    });
    function setViewName(viewName) {
        gridViewName = viewName;
    }
    function getViewName() {
        return gridViewName;
    }
    function updateGrid() {
        var grid = $("#EPMSDashboardGrid").data("kendoGrid");
        var toolBar = $("#EPMSDashboardGrid .k-grid-toolbar").html();
        $(function (e) {
            var options;
            $.ajax({
                type: "POST",
                url: '@Url.Action("GetOptions", "EPMSDashboard")',
                traditional: true,
                data: JSON.stringify({ gridName: "EPMSDashboardGrid", gridViewName: gridViewName }),
                dataType: 'html',
                contentType: "application/json; charset=utf-8",
                processData: false
            }).success(function (e) {
                if (e) {
                    grid.setOptions(JSON.parse(e));
                    $("#EPMSDashboardGrid .k-grid-toolbar").html(toolBar);
                    $("#EPMSDashboardGrid .k-grid-toolbar").addClass("k-grid-top");
                    $("#saveState").click(function (e) {
                        e.preventDefault();
                        e.stopPropagation();
                        var viewName = getViewName();
                        if (viewName == "") {
                            callModal(e);
                        }
                        else {
                            saveCurrentView(viewName);
                        }
                    });
                    $("#addNewView").click(function (e) {
                        e.preventDefault();
                        e.stopPropagation();
                        callModal(e);
                    });
                    $("#resetSettings").click(function (e) {
                        e.preventDefault();
                        e.stopPropagation();
                        setViewName("Original");
                        updateGrid();
                        var zindex = parseInt($($("li.k-state-active")[0].previousSibling).css("z-index")) - 1
                        $("li.k-state-active").css("z-index", zindex);
                        $("li.k-state-active").css("background-color", "#fff");
                        $("li.k-state-active>span.k-link").css("color", "#428bca");
                        $("li.k-state-active").removeClass("k-state-active");
                        setViewName("");
                    });
                    $("#deleteView").click(function (e) {
                        e.preventDefault();
                        data = {}
                        var viewName = getViewName();
                        data["gridViewName"] = viewName;
                        if (viewName != "") {
                            $.ajax({
                                type: "POST",
                                url: '@Url.Action("ClearSettings", "EPMSDashboard")',
                                traditional: true,
                                data: JSON.stringify(data),
                                dataType: 'html',
                                contentType: "application/json; charset=utf-8",
                                processData: false
                            }).success(function () {
                                setViewName("Original");
                                updateGrid();
                                setViewName("");
                                loadTabs();
                                $("#statusView").html("Successfully deleted");
                                $("#statusView").show();
                                $("#statusView").fadeOut(3000);
                            });
                        }
                    });
                } else {
                    grid.dataSource.read();
                }
            });
        });

    }
    function getCurrentOptions() {
        var grid = $("#EPMSDashboardGrid").data("kendoGrid");
        var options = kendo.stringify(grid.getOptions());
        var searchOptions = $("#searchbox").val();
        return { options: options, searchbox: searchOptions };
    }
    function addData() {
        return { searchbox: $("#searchbox").val() };
    };
    $(document).ready(function () {
        var typingTimer;
        var doneTypingInterval = 1000;
        $("#searchbox").keydown(function (e) {
            clearTimeout(typingTimer);
            var evt = e || window.event
            if ($('#searchbox').val) {
                if (evt.keyCode === 13) {
                    PostSearchForm();
                } else {
                    typingTimer = setTimeout(PostSearchForm, doneTypingInterval);
                }
            }
        });
    });
    function PostSearchForm() {
        $("#EPMSDashboardGrid").data("kendoGrid").dataSource.read();
    };
    function saveandclose(e) {
        var presses = [];
        var grid = $("#EPMSDashboardGrid").data("kendoGrid");
        var myWindow = $("#configuration");
        $('#configModal input[type="checkbox"]:checked').each(function () {
            presses.push($("label[for='" + this.id + "']").html());
        })
        var filter = { logic: "or", filters: [] };
        var newFilters = [];
        console.log(presses);
        for (var i = 0; i < presses.length; i++) {
            if (presses[i].includes("&amp;")) {
                presses[i] = presses[i].replace("&amp;", "&");
            }
            if (presses[i] == "No Press") {
                var newFilter = { field: "PressCC", operator: "eq", value: "" };
            } else {
                var newFilter = { field: "PressCC", operator: "eq", value: presses[i] };
            }
            filter.filters.push(newFilter);
        }
        grid.dataSource.query({ filter: filter })
    }
</script>

<button id="myModalButton" type="button" class="btn btn-info btn-lg" data-toggle="modal" data-target="#myModal" style="display:none;"></button>

<div class="content" data-tab="w20-EPMSDashboard-Job" id="epmsDashboard">
    <div id="myModal" class="modal fade" role="dialog">
        <div class="modal-dialog">
            <div class="modal-content">
                <div class="modal-header">
                    <button type="button" class="close" data-dismiss="modal">&times;</button>
                    <h4 class="modal-title">Save Grid View</h4>
                </div>
                <div class="modal-body">
                    <label for="GridViewName">Give the grid a name:</label>
                    @Html.Kendo().TextBox().Name("GridViewName").HtmlAttributes(new { id = "gridViewName", placeholder = "e.g. 'Warehouse View #1'", style = "width:100%;", required = "required" })
                </div>
                <div class="modal-footer">
                    <button id="submitGridViewName" type="submit" class="btn btn-default" data-dismiss="modal">Submit</button>
                </div>
            </div>
        </div>
    </div>
    <section class="content-header">
        <h1 id="jobHeader">
            EPMS Job Dashboard
            <button type='button' class='k-button pull-right bg-white' id='airPort' onclick='airPortMode()' title='Full Screen' style="padding:5px;line-height:12px;"><i class='fa fa-arrows-alt' aria-hidden='true'></i></button>
        </h1>
    </section>
    <section class="content">
        <!-- Search box -->
        <div class="box box-primary collapsed-box">
            <div class="box-header with-border">
                <div class="box-title">
                    <div class="input-group">
                        <span class="input-group-addon" style="border-top-left-radius:3px;border-bottom-left-radius:3px;"><i class="fa fa-search"></i></span>
                        <input type="text" id="searchbox" name="SearchText" class="form-control" placeholder="Search">
                        <div class="input-group-btn">
                            <div id="filterButton"><button class="btn btn-primary btn-warning advSearch" title="Show Filter" id='showFilter' data-widget="collapse" style="border-radius:0;border-bottom-right-radius:3px;border-top-right-radius:3px;">Advanced <span class="fa fa-caret-down" aria-hidden='true'></span></button></div>
                            <div id="showFilterButton"><button class="btn btn-primary btn-warning advSearch" title="Hide Filter" id='hideFilter' data-widget="collapse" style="border-radius:0;border-bottom-right-radius:3px;border-top-right-radius:3px;">Advanced <span class="fa fa-caret-up" aria-hidden='true'></span></button></div>
                        </div>
                    </div>
                </div>
            </div><!-- /.box-header -->
            <div class="box-body">
                <div id="configModal">
                    <ul class="pressList" style="font-size:11pt; list-style: none; columns:6; -webkit-columns:6; -moz-columns:6">
                        @Html.Action("Configuration", "EPMSDashboard")
                    </ul>
                    <button id="saveandclose" title="Apply Filter" style="font-size:14px;float:right;margin-right:15px;" class="k-primary k-button" onclick="saveandclose()"><i class="fa fa-filter"></i> Apply</button>
                </div>
            </div>
        </div>
        <div id="tabstrip">
            <ul id="tabStripUl" style="text-align:right;">
                <li></li>
            </ul>
        </div>
        <div id="EPMSDashboard">
            @(Html.Kendo().Grid<WBGLibrary.EPMSDashboard>()
            .Name("EPMSDashboardGrid")
            .ToolBar(t =>
            {
                t.Excel();
                t.Custom().Name("SaveSettings").Text("&#128190; Save Settings").HtmlAttributes(new { id = "saveState", style = "position:relative;top:1px;" });
                t.Custom().Name("SaveAsNewView").Text("&#128196; Add New View").HtmlAttributes(new { id = "addNewView", @class = "pull-right", style = "position:relative;top:1px;" });
                t.Custom().Name("ResetSettings").Text("&#8617; Reset Settings").HtmlAttributes(new { id = "resetSettings", style = "position:relative;top:1px;" });
                t.Custom().Name("DeleteView").Text("&#9888; Delete View").HtmlAttributes(new { id = "deleteView", style = "color:#dd2222;position:relative;top:1px;", });
                t.Custom().Name("StatusView").Text("&#10004; Successfully Saved").HtmlAttributes(new { id = "statusView", style = "background-color:transparent;color:#3c763d;position:relative;top:1px;display:none;border:0;" });
            })
            .Excel(e => e
                .FileName("EPMSJobDashboard.xlsx")
                .Filterable(true)
                .AllPages(true)
            )
            .Resizable(resize => resize.Columns(true))
            .Selectable()
            .Reorderable(reorder => reorder.Columns(true))
            .Columns(columns =>
            {
                columns.Bound(rt => rt.Id).Hidden();
                columns.Bound(rt => rt.RUSH).Width("90px").ClientTemplate("# if(RUSH=='True'){#<i class='fa fa-check' aria-hidden='true'></i>#} else{# #}#").Hidden(true);
                columns.Bound(rt => rt.PlantID).Width("75px").Title("Plant");
                columns.Bound(rt => rt.JobNumber).Title("Job#").Width("90px");
                columns.Bound(rt => rt.DueDate).Width("120px").Format("{0:MM/dd/yy}");
                columns.Bound(rt => rt.DeliveryDate).Width("125px").Format("{0:MM/dd/yy}").Hidden(true);
                columns.Bound(rt => rt.CNum).Title("C#").Width("60px");
                columns.Bound(rt => rt.ProdType).Width("150px");
                columns.Bound(rt => rt.PressCC).Width("150px").Filterable(false).Hidden(true);
                columns.Bound(rt => rt.Customer).Width("200px").HtmlAttributes(new { title = "#= Customer #" });
                columns.Bound(rt => rt.JobDescription).Width("200px").HtmlAttributes(new { title = "#= JobDescription #" });
                columns.Bound(rt => rt.Quantity).Width("95px").Hidden(true);
                columns.Bound(rt => rt.LastCC).Width("130px").Hidden(true);
                columns.Bound(rt => rt.NextCC).Width("130px");
                columns.Bound(rt => rt.StartDate).Width("120px").Format("{0:MM/dd/yy}").Hidden(true);
                columns.Bound(rt => rt.StartTime).Width("120px").Format("{0:HH:mm}").Hidden(true);
                columns.Bound(rt => rt.Paper).Width("200px").HtmlAttributes(new { title = "#= Paper #" });
                columns.Bound(rt => rt.PreTrim).Width("100px").ClientTemplate("# if(PreTrim=='True'){#<i class='fa fa-check' aria-hidden='true'></i>#} else{# #}#").Hidden(true);
                columns.Bound(rt => rt.PO).Width("120px").Hidden(true).Filterable(false).Hidden(true);
                columns.Bound(rt => rt.POReceipt).Width("120px").Hidden(true).Filterable(false).Hidden(true);
                columns.Bound(rt => rt.ComponentDescription).Width("200px").HtmlAttributes(new { title = "#= ComponentDescription #" });
                columns.Bound(rt => rt.CSR).Width("150px");
                columns.Bound(rt => rt.JobNotes).Width("120px").Title("Comments");
                columns.Bound(rt => rt.OutOnProof).Width("120px").ClientTemplate("#if(OutOnProof=='True'){#<i class='fa fa-check' aria-hidden='true'></i>#} else {# #}#").Title("On Proof");
                columns.Bound(rt => rt.PrintBy).Width("100px").ClientTemplate("#if(PrintBy=='Thu Dec 31 2099 00:00:00 GMT-0800 (Pacific Standard Time)'){# #} else{# #=kendo.toString(PrintBy,'MM/dd/yy')# #}#");
                columns.Bound(rt => rt.PressTime).Width("110px").Hidden(true);
                columns.Bound(rt => rt.CountInks).Width("75px").Title("Inks").Hidden(true);
                columns.Bound(rt => rt.Trim).Width("75px").Hidden(true);
                columns.Bound(rt => rt.Score).Width("80px").Hidden(true);
                columns.Bound(rt => rt.Fold).Width("75px").Hidden(true);
                columns.Bound(rt => rt.Stitch).Width("80px").Hidden(true);
                columns.Bound(rt => rt.Glue).Width("75px").Hidden(true);
                columns.Bound(rt => rt.Insert).Width("80px").Hidden(true);
                columns.Bound(rt => rt.InkJet).Width("85px").Hidden(true);
                columns.Bound(rt => rt.Pad).Width("70px").Hidden(true);
                columns.Bound(rt => rt.Coil).Width("75px").Hidden(true);
                columns.Bound(rt => rt.Shrink).Width("85px").Hidden(true);
                columns.Bound(rt => rt.OS).Width("100px").ClientTemplate("#if(OS==0){# #} else{# #=OS# #}#").Hidden(true);
                columns.Bound(rt => rt.ShipVia).Width("100px").Hidden(true);
                columns.Bound(rt => rt.ShipInstructions).Width("200px").HtmlAttributes(new { title = "#= ShipInstructions #" }).Hidden(true);

            })
            .Navigatable()
            .Sortable()
            .Filterable(filterable => filterable
                .Extra(false)
                .Operators(operators => operators
                .ForString(str => str.Clear()
                .StartsWith("Starts with")
                .IsEqualTo("Is equal to")
                .IsNotEqualTo("Is not equal to")
                ))
                )
            .Scrollable()
            .Pageable(pageable => pageable
                .Refresh(true)
                .PageSizes(false)
                .ButtonCount(5))
            .ColumnMenu()
            .Events(events => events
                .DataBound("rushChange")
            )
            .DataSource(dataSource => dataSource
                .Ajax()
                .PageSize(20)
                .Sort(sort =>
                {
                    sort.Add("RUSH").Descending();
                    sort.Add("OutOnProof").Ascending();
                    sort.Add("DueDate").Ascending();
                })
                .Model(model =>
                {
                    model.Id(p => p.Id);
                    model.Field(p => p.RUSH);
                    model.Field(p => p.PlantID);
                    model.Field(p => p.JobNumber);
                    model.Field(p => p.DueDate);
                    model.Field(p => p.DeliveryDate);
                    model.Field(p => p.CNum);
                    model.Field(p => p.ProdType);
                    model.Field(p => p.PressCC);
                    model.Field(p => p.Customer);
                    model.Field(p => p.JobDescription);
                    model.Field(p => p.Quantity);
                    model.Field(p => p.LastCC);
                    model.Field(p => p.NextCC);
                    model.Field(p => p.StartDate);
                    model.Field(p => p.StartTime);
                    model.Field(p => p.Paper);
                    model.Field(p => p.PreTrim);
                    model.Field(p => p.PO);
                    model.Field(p => p.POReceipt);
                    model.Field(p => p.ComponentDescription);
                    model.Field(p => p.CSR);
                    model.Field(p => p.JobNotes);
                    model.Field(p => p.OutOnProof);
                    model.Field(p => p.PrintBy);
                    model.Field(p => p.PressTime);
                    model.Field(p => p.CountInks);
                    model.Field(p => p.Trim);
                    model.Field(p => p.Score);
                    model.Field(p => p.Fold);
                    model.Field(p => p.Stitch);
                    model.Field(p => p.Glue);
                    model.Field(p => p.Insert);
                    model.Field(p => p.InkJet);
                    model.Field(p => p.Pad);
                    model.Field(p => p.Coil);
                    model.Field(p => p.Shrink);
                    model.Field(p => p.OS);
                    model.Field(p => p.ShipVia);
                    model.Field(p => p.ShipInstructions);
                })
            .Read(read => read.Action("GetJobs", "EPMSDashboard").Data("getCurrentOptions"))
            )
            )
        </div>
    </section>
</div>

<script>
    $(document).ready(function () {
        $("#saveandclose").kendoButton();
        loadTabs();
    });
    function loadTabs() {
        $("#tabstrip").kendoTabStrip({
            animation: {
                open: {
                    effects: "fadeIn"
                }
            }
        });
        $.ajax({
            type: "POST",
            url: '@Url.Action("GetViews", "EPMSDashboard")',
            traditional: true,
            data: JSON.stringify({ gridName: "EPMSDashboardGrid" }),
            dataType: 'html',
            contentType: "application/json; charset=utf-8",
            processData: false
        }).success(function (e) {
            var results = JSON.parse(e);
            $("#tabStripUl").empty();
            var tabstripWidth = 120 * results.length > 500 ? 500 : 120 * results.length;
            $('#addNewView').css("left", results.length * 2);
            $("#tabstrip").css("width", tabstripWidth);
            $("#tabstrip").kendoTabStrip({
                animation: {
                    open: {
                        effects: "fadeIn"
                    }
                }
            });
            for (i = 0; i < results.length; i++) {
                console.log(results[i]);
                $("#tabStripUl").prepend("<li id='tab" + i + "' data-options='" + results[i].gridOptions + "' style='position:relative;left:" + (10 * i) + "px;z-index:" + (1000 - i) + ";background-color:#fff;' class='tabStrip'>" + results[i].gridOptions + "</li>");
                $("#tab" + i).click(function (event) {
                    for (j = 0; j < results.length; j++) {
                        var num = 1000 - j;
                        $("#tab" + j).css("z-index", num);
                        $("#tab" + j).css("background-color", "#fff");
                        $("#tab" + j + ">span.k-link").css("color", "#428bca");
                    }
                    $(this).css("z-index", "1001");
                    $(this).css("background-color", "#428bca");
                    $(this).css("border-bottom-color", "#ccc");
                    $($(this)[0].children[1]).css("color", "#fff");
                    var gridViewName = $(this).attr("data-options");
                    setViewName(gridViewName);
                    updateGrid();
                });
                var tabStrip = $("#tabstrip").kendoTabStrip().data("kendoTabStrip");
            }
            if (results.length == 1) {
                $("#tabStripUl").css("margin-left", "");
                $("#tabStripUl").css("margin-right", "");

            }
        });
    }

    function clearSettings() {
        data = {}
        var viewName = getViewName();
        data["gridViewName"] = viewName;
        if (viewName != "") {
            $.ajax({
                type: "POST",
                url: '@Url.Action("ClearSettings", "EPMSDashboard")',
                traditional: true,
                data: JSON.stringify(data),
                dataType: 'html',
                contentType: "application/json; charset=utf-8",
                processData: false
            }).success(function () {
                $("#resetSettings").click();
            });
        }
    }
    function getPOClass(po, poReceipt) {
        if ((po != null) && (poReceipt == null)) {
            return "yellow";
        }
        if ((po != null) && (poReceipt != null)) {
            return "green";
        }
    }
    //function getOutOnProofClass(outOnProof) {
    //    if (outOnProof=='True') {
    //        return "orange";
    //    }
    //}
    function rushChange(RUSH) {
        var grid = $("#EPMSDashboardGrid").data("kendoGrid");
        var data = grid.dataSource.data();
        $.each(data, function (i, row) {
            var rush = row.RUSH;
            if (rush == '1') {
                $('tr[data-uid="' + row.uid + '"] td').css("background-color", "pink");
                var options = kendo.stringify(grid.getOptions());
                $.ajax({
                    type: "POST",
                    url: '@Url.Action("StoreOptions", "EPMSDashboard")',
                    traditional: true,
                    data: JSON.stringify({ gridOptions: options, gridName: "EPMSDashboardGrid" }),
                    dataType: 'html',
                    contentType: "application/json; charset=utf-8",
                    processData: false
                });
            }
        });
        $.each(data, function (i, row) {
            var outOnProof = row.OutOnProof;
            if (outOnProof == 'True') {
                $('tr[data-uid="' + row.uid + '"] td').css("background-color", "orange");
            }
        });
        if (grid.dataSource._filter != undefined) {
            if (grid.dataSource._filter.filters.length > 0) {
                for (var i = 0; i < grid.dataSource._filter.filters.length; i++) {
                        var pressFilter = ("" + grid.dataSource._filter.filters[i].value).replace(/\s/g, "");
                        if (pressFilter == "") {
                            $("input[name='NoPress']").prop("checked", true);
                        } else {
                            $("input[name='" + pressFilter + "']").prop("checked", true);
                        }
                }
            }
        }
        var poIndex = this.wrapper.find(".k-grid-header [data-field=" + "PO" + "]").index();
        var poReceiptIndex = this.wrapper.find(".k-grid-header [data-field=" + "POReceipt" + "]").index();
        var paperIndex = this.wrapper.find(".k-grid-header [data-field=" + "Paper" + "]").index();
        var rushIndex = this.wrapper.find(".k-grid-header [data-field=" + "RUSH" + "]").index();
        //var outOnProofIndex = this.wrapper.find(".k-grid-header [data-field=" + "OutOnProof" + "]").index();

        var dataItems = RUSH.sender.dataSource.view();
        for (var j = 0; j < dataItems.length; j++) {

            var po = dataItems[j].get("PO");
            var rush = dataItems[j].get("RUSH");
            var poReceipt = dataItems[j].get("POReceipt")
            //var outOnProof = dataItems[j].get("OutOnProof")
            var paper = dataItems[j].get("Paper")

            var row = RUSH.sender.tbody.find("[data-uid='" + dataItems[j].uid + "']");

            if (rush=='True') {
                row.addClass("pink");
            }

            var poCell = row.children().eq(poIndex);
            var poReceiptCell = row.children().eq(poReceiptIndex);
            //var outOnProofCell = row.children().eq(outOnProofIndex);
            var paperCell = row.children().eq(paperIndex);

            poReceiptCell.addClass(getPOClass(po, poReceipt));
            poCell.addClass(getPOClass(po, poReceipt));
            paperCell.addClass(getPOClass(po, poReceipt));
            //outOnProofCell.addClass(getOutOnProofClass(outOnProof));
        }
    }
    function rushAirportChange(RUSH) {
        var grid = $("#EPMSAirportGrid").data("kendoGrid");
        var data = grid.dataSource.data();
        $.each(data, function (i, row) {
            var rush = row.RUSH;
            if (rush == '1') {
                $('tr[data-uid="' + row.uid + '"] td').css("background-color", "pink");
                }
        });
    }
    $("#addNewView").click(function (e) {
        e.preventDefault();
        e.stopPropagation();
        callModal(e);
    });
    $("#resetSettings").click(function (e) {
        e.preventDefault();
        e.stopPropagation();
        setViewName("Original");
        updateGrid();
        var zindex = parseInt($($("li.k-state-active")[0].previousSibling).css("z-index")) - 1
        $("li.k-state-active").css("z-index", zindex);
        $("li.k-state-active").css("background-color", "#fff");
        $("li.k-state-active>span.k-link").css("color", "#428bca");
        $("li.k-state-active").removeClass("k-state-active");
        setViewName("");
    });
    $("#deleteView").click(function (e) {
                        e.preventDefault();
                        data = {}
                        var viewName = getViewName();
                        data["gridViewName"] = viewName;
                        if (viewName != "") {
                            $.ajax({
                                type: "POST",
                                url: '@Url.Action("ClearSettings", "EPMSDashboard")',
                                traditional: true,
                                data: JSON.stringify(data),
                                dataType: 'html',
                                contentType: "application/json; charset=utf-8",
                                processData: false
                            }).success(function () {
                                setViewName("Original");
                                updateGrid();
                                setViewName("");
                                loadTabs();
                                $("#statusView").html("Successfully deleted");
                                $("#statusView").show();
                                $("#statusView").fadeOut(3000);
                            });
                        }

                    });
    $("#saveState").click(function (e) {
        e.preventDefault();
        e.stopPropagation();
        var viewName = getViewName();
        if (viewName == "") {
            callModal(e);
        }
        else {
            saveCurrentView(viewName);
        }
    })
    function callModal(e) {
        e.preventDefault();
        $("#myModalButton").click();
    }
    $("#submitGridViewName").click(function (e) {
        saveState(e);
    })
    function saveCurrentView(viewName) {
        var grid = $("#EPMSDashboardGrid").data("kendoGrid");
        var data = grid.dataSource.data();
        $.each(data, function (i, row) {
            var rush = row.RUSH;
            if (rush == '1') {
                $('tr[data-uid="' + row.uid + '"] td').css("background-color", "pink");
            }
        });
        data = {}
        data["gridOptions"] = kendo.stringify(grid.getOptions());
        data["gridName"] = "EPMSDashboardGrid";
        data["gridViewName"] = viewName;
        $.ajax({
            type: "POST",
            url: '@Url.Action("StoreOptions", "EPMSDashboard")',
            traditional: true,
            data: JSON.stringify(data),
            dataType: 'html',
            contentType: "application/json; charset=utf-8",
            processData: false
        }).success(function (e) {
            $("#statusView").show();
            $("#statusView").fadeOut(2000);
        });
    };

    function saveState(e) {
        var grid = $("#EPMSDashboardGrid").data("kendoGrid");
        var data = grid.dataSource.data();
        var viewName = $("#gridViewName").val();
        setViewName(viewName);
        $.each(data, function (i, row) {
            var rush = row.RUSH;
            if (rush == '1') {
                $('tr[data-uid="' + row.uid + '"] td').css("background-color", "pink");
            }
        });
        data = {}
        data["gridOptions"] = kendo.stringify(grid.getOptions());
        data["gridName"] = "EPMSDashboardGrid";
        data["gridViewName"] = viewName;
        $.ajax({
            type: "POST",
            url: '@Url.Action("StoreOptions", "EPMSDashboard")',
            traditional: true,
            data: JSON.stringify(data),
            dataType: 'html',
            contentType: "application/json; charset=utf-8",
            processData: false
        }).success(function (e) {
            loadTabs();
            var existCondition = setInterval(function () {
                if ($("#tabStripUl").find("[data-options='" + getViewName() + "']").length) {
                    clearInterval(existCondition);
                    $("#tabStripUl").find("[data-options='" + getViewName() + "']").click();
                }
            }, 100);

        });
    };

    @*$(document).ready(function () {
        var grid = $("#EPMSDashboardGrid").data("kendoGrid");
        var toolBar = $("#EPMSDashboardGrid .k-grid-toolbar").html();
        $(function (e) {
            var options;
            $.ajax({
                type: "POST",
                url: '@Url.Action("GetOptions", "EPMSDashboard")',
                traditional: true,
                data: JSON.stringify({ gridName: "EPMSDashboardGrid", gridOptions: "View 1" }),
                dataType: 'html',
                contentType: "application/json; charset=utf-8",
                processData: false
            }).success(function (e) {
                if (e) {
                    grid.setOptions(JSON.parse(e));
                    $("#EPMSDashboardGrid .k-grid-toolbar").html(toolBar);
                    $("#EPMSDashboardGrid .k-grid-toolbar").addClass("k-grid-top");
                    //$("#saveState").click(function (event) {
                    //    callModal(event);
                    //})
                } else {
                    grid.dataSource.read();
                }
            });
        });
    });*@
    $(document).ready(function () {
        var refreshId = setInterval(function () {
        var grid = $("#EPMSDashboardGrid").data("kendoGrid");
        //300 seconds
        grid.dataSource.read();
        }, 300000);
    });
    function airPortMode() {
        var fullScreenElement = document.getElementById("EPMSDashboard")
        fullScreenElement.webkitRequestFullscreen()
        $("#EPMSDashboard").css({
            "width": "100%",
            "height": "100% !important"
        });
        var refreshId = setInterval(function () {
            //300 seconds
            var grid = $("#EPMSDashboardGrid").data("kendoGrid");
            grid.dataSource.read();
        }, 300000);
        if (!document.mozFullScreen && !document.webkitFullScreen) {
            if (fullScreenElement.mozRequestFullScreen) {
                fullScreenElement.mozRequestFullScreen();
            } else {
                fullScreenElement.webkitRequestFullScreen(Element.ALLOW_KEYBOARD_INPUT);
            }
            $("#epmsairportContainer").show();
        }
    }
    if (document.addEventListener) {
        document.addEventListener('webkitfullscreenchange', exitHandler, false);
        document.addEventListener('mozfullscreenchange', exitHandler, false);
        document.addEventListener('fullscreenchange', exitHandler, false);
        document.addEventListener('MSFullscreenChange', exitHandler, false);
    }
    function exitHandler() {
        if (!document.webkitIsFullScreen && !document.mozFullScreen && !document.msFullscreenElement) {
            $('#epmsairportContainer').hide();
            console.log("hide");
            //clearTimeout(timer);
        }
    }
</script>
