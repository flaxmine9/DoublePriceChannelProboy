using ScriptSolution;
using ScriptSolution.Indicators;
using ScriptSolution.Model;
using System;

namespace PriceChannelProboyTask
{
    public class DoublePriceChennel : Script
    {
        public CreateInidicator priceChannelFast = new CreateInidicator(EnumIndicators.PriceChannel, 0, "Быстрый");
        public CreateInidicator priceChannelSlow = new CreateInidicator(EnumIndicators.PriceChannel, 0, "Медленный");

        public ParamOptimization channelFast = new ParamOptimization(value: 5, startStep: 5, endStep: 15, stepOptimization: 2, nameParam: "Малый период");
        public ParamOptimization channelSlow = new ParamOptimization(value: 10, startStep: 10, endStep: 25, stepOptimization: 2, nameParam: "Большой период");
        
        public ParamOptimization minDistance = new ParamOptimization(value: 10, startStep: 10, endStep: 30, stepOptimization: 5, nameParam: "Минимальное расстояние");
        public ParamOptimization maxDistance = new ParamOptimization(value: 50, startStep: 50, endStep: 100, stepOptimization: 5, nameParam: "Максимальное расстояние");

        public ParamOptimization widthChannel = new ParamOptimization(value: 30, startStep: 30, endStep: 50, stepOptimization: 5, nameParam: "Ширина малого канала");
        
        public ParamOptimization valueDistance = new ParamOptimization(value: 5, startStep: 5, endStep: 30, stepOptimization: 1, nameParam: "Расстояние между линиями");

        public ParamOptimization Proboy = new ParamOptimization(2, 2, 10, 1, "Величина пробоя",
            "Величина на которую должен пробиться уровень. Данная величина умножается на шаг цены.");

        public ParamOptimization reverse = new ParamOptimization(valBool: false, nameParam: "Reverse");

        private string _dir { get; set; }
        private double _high = 0;
        private double _low = 0;

        public override void Execute()
        {
            for (var bar = IndexBar; bar < CandleCount - 1; bar++)
            {
                if (priceChannelFast.param.LinesIndicators[0].LineParam[0].Value < bar
                    && priceChannelSlow.param.LinesIndicators[0].LineParam[0].Value < bar)
                {
                    var priceUpFast = priceChannelFast.param.LinesIndicators[0].PriceSeries;
                    var priceDownFast = priceChannelFast.param.LinesIndicators[1].PriceSeries;

                    var priceUpSlow = priceChannelSlow.param.LinesIndicators[0].PriceSeries;
                    var priceDownSlow = priceChannelSlow.param.LinesIndicators[1].PriceSeries;

                    // Логика в лонг позицию
                    if (Candles.HighSeries[bar] >= priceUpFast[bar]
                        && Candles.HighSeries[bar] >= priceUpSlow[bar]
                        && !CrossOver(priceChannelFast.param.LinesIndicators[0].PriceSeries,
                            priceChannelSlow.param.LinesIndicators[0].PriceSeries, bar - 1)
                        && priceChannelFast.param.LinesIndicators[0].PriceSeries[bar] - priceChannelFast.param.LinesIndicators[1].PriceSeries[bar] < widthChannel.Value
                        && priceUpSlow[bar] - priceUpFast[bar] > valueDistance.Value && priceUpSlow[bar] - priceUpFast[bar] < maxDistance.Value
                        && _dir != "Long")
                    {
                        // если галочка не активирована, то лонг
                        if (!reverse.ValueBool)
                        {
                            _high = Candles.HighSeries[bar];
                            _low = 0;
                            _dir = "Long";
                        }
                        else
                        {
                            _low = 0;
                            _high = Candles.HighSeries[bar];
                            _dir = "Short";
                        }
                    }

                    if(Candles.LowSeries[bar] <= priceDownFast[bar]
                        && Candles.LowSeries[bar] <= priceDownSlow[bar]
                        && !CrossUnder(priceChannelFast.param.LinesIndicators[1].PriceSeries,
                            priceChannelSlow.param.LinesIndicators[1].PriceSeries, bar - 1)
                        && priceChannelFast.param.LinesIndicators[0].PriceSeries[bar] - priceChannelFast.param.LinesIndicators[1].PriceSeries[bar] < widthChannel.Value
                        && priceDownFast[bar] - priceDownSlow[bar] > valueDistance.Value && priceDownFast[bar] - priceDownSlow[bar] > minDistance.Value
                        && _dir != "Short")
                    {
                        // если галочка не активирована, то шорт
                        if (!reverse.ValueBool)
                        {
                            _low = Candles.LowSeries[bar];
                            _dir = "Short";
                            _high = 0;
                        }
                        else
                        {
                            _low = Candles.LowSeries[bar];
                            _dir = "Long";
                            _high = 0;
                        }
                    }

                    // Логика входа при лонг сигнале без активированной галочки
                    if (_dir == "Long" && reverse.ValueBool == false && _high > 0.000000001)
                    {
                        if(ShortPos.Count > 0)
                        {
                            CoverAtStop(bar + 1, ShortPos[0], priceUpSlow[bar + 1] + Proboy.ValueInt * FinInfo.Security.MinStep, "Переворот.");   
                        }

                        BuyGreater(bar + 1, _high + Proboy.ValueInt * FinInfo.Security.MinStep, 1,
                                "Переворот. Открытие длинной позиции");
                    }

                    // Логика входа при шорт сигнале без активированной галочки
                    if (_dir == "Short" && reverse.ValueBool == false && _low > 0.000000001)
                    {
                        if(LongPos.Count > 0)
                        {
                            // закрываем лонг позицию
                            SellAtStop(bar + 1, LongPos[0], priceDownSlow[bar + 1] - Proboy.ValueInt * FinInfo.Security.MinStep,
                                "Переворот.");
                        }
                        // открываем шорт позицию
                        ShortLess(bar + 1, _low - Proboy.ValueInt * FinInfo.Security.MinStep, 1,
                            "Переворот. Открытие короткой позиции");
                    }

                    // Логика входа при лонг сигнале с активированной галочкой
                    if (_dir == "Long" && reverse.ValueBool == true && _high > 0.000000001)
                    {
                        if(LongPos.Count > 0)
                        {
                            // закрываем лонг позицию
                            SellAtStop(bar + 1, LongPos[0], priceUpSlow[bar + 1] + Proboy.ValueInt * FinInfo.Security.MinStep,
                                "Переворот.");
                        }
                        // открываем короткую позицию
                        ShortLess(bar + 1, _high - Proboy.ValueInt * FinInfo.Security.MinStep, 1,
                            "Переворот. Открытие короткой позиции");
                    }

                    // Логика входа при шорт сигнале с активированной галочкой
                    if (_dir == "Short" && reverse.ValueBool == true && _low > 0.000000001)
                    {
                        if (ShortPos.Count > 0)
                        {
                            // закрываем шорт позицию
                            CoverAtStop(bar + 1, ShortPos[0], priceDownSlow[bar + 1] - Proboy.ValueInt * FinInfo.Security.MinStep, "Переворот.");
                        }
                        // открываем лонг позицию
                        BuyGreater(bar + 1, _low + Proboy.ValueInt * FinInfo.Security.MinStep, 1,
                                "Переворот. Открытие длинной позиции");
                    }


                    if (bar > 2)
                    {
                        ParamDebug("Большой Верх. канал тек.",
                            Math.Round(priceUpSlow[bar + 1], 4));
                        ParamDebug("Большой Ниж. канал тек.",
                            Math.Round(priceDownSlow[bar + 1], 4));

                        ParamDebug("Малый Верх. канал тек.",
                            Math.Round(priceUpFast[bar + 1], 4));
                        ParamDebug("Малый Ниж. канал тек.",
                            Math.Round(priceDownFast[bar + 1], 4));
                    }


                    if (LongPos.Count != 0 || ShortPos.Count != 0)
                    {
                        if (LongPos.Count != 0)
                            _dir = "Long";
                        if (ShortPos.Count != 0)
                            _dir = "Short";

                        SendStandartStopFromForm(bar + 1, "");
                        SendTimePosCloseFromForm(bar + 1, "");
                        SendClosePosOnRiskFromForm(bar + 1, "");
                    }
                    else
                    {
                        _dir = "";
                    }

                    ParamDebug("Направление", _dir);

                }
            }
        }

        public override void GetAttributesStratetgy()
        {
            DesParamStratetgy.Version = "1.0.0.1";
            DesParamStratetgy.DateRelease = "21.06.2015";
            DesParamStratetgy.DateChange = "14.06.2019";
            DesParamStratetgy.Author = "Flax";
            DesParamStratetgy.Description = "Тестовая стратегия";
            DesParamStratetgy.Change = "";
            DesParamStratetgy.NameStrategy = "DoublePriceChannelProboy";
        }
    }
}
