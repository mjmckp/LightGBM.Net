﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace LightGBMNet.Train.Test
{
    public class BoosterTest
    {
        [Fact]
        public void Create()
        {
            var rand = new Random();
            for (int test = 0; test < 100; ++test)
                using (var dataSet = DatasetTest.CreateRandom(rand))
                {
                    var pms = new Parameters();
                    using (var booster = new Booster(pms, dataSet))
                    {
                    }
                }
        }

        [Fact]
        public void ResetTrainingData()
        {
            var rand = new Random();
            for (int test = 0; test < 100; ++test)
                using (var dataSet = DatasetTest.CreateRandom(rand))
                {
                    var pms = new Parameters();
                    pms.Common.Verbosity = VerbosityType.Error;
                    using (var booster = new Booster(pms, dataSet))
                    {
                        // must have the same characteristics for binning to be able to do this.
                        var dataSet2 = new Dataset(dataSet, dataSet.NumRows / 2);
                        Assert.Equal(dataSet.NumFeatures,booster.NumFeatures);
                        Assert.Equal(dataSet.NumRows,booster.GetNumPredict(0));
                        booster.ResetTrainingData(dataSet2);
                        Assert.Equal(dataSet2.NumFeatures,booster.NumFeatures);
                        Assert.Equal(dataSet.NumRows / 2, dataSet2.NumRows);
                        Assert.Equal(dataSet2.NumRows,booster.GetNumPredict(0));
                    }
                }
        }

        [Fact]
        public void GetNumPredict_GetPredict()
        {
            var rand = new Random();
            for (int test = 0; test < 100; ++test)
                using (var dataSet = DatasetTest.CreateRandom(rand))
                {
                    var pms = new Parameters();
                    pms.Objective.Metric = MetricType.MultiLogLoss;
                    pms.Objective.NumClass = rand.Next(2, 4);
                    pms.Objective.Objective = ObjectiveType.MultiClass;
                    using (var booster = new Booster(pms, dataSet))
                    {
                        var numPredict = booster.GetNumPredict(0);
                        Assert.Equal(dataSet.NumRows * pms.Objective.NumClass, numPredict);
                        var rslts = booster.GetPredict(0);
                        Assert.Equal(rslts.Length, numPredict);
                    }
                }
        }


        [Fact]
        public void NumFeatures()
        {
            var rand = new Random();
            for (int test = 0; test < 100; ++test)
                using (var dataSet = DatasetTest.CreateRandom(rand))
                {
                    var pms = new Parameters();
                    using (var booster = new Booster(pms, dataSet))
                    {
                        Assert.Equal(booster.NumFeatures, dataSet.NumFeatures);
                    }
                }
        }


        [Fact]
        public void FeatureNames()
        {
            var rand = new Random();
            for (int test = 0; test < 100; ++test)
                using (var dataSet = DatasetTest.CreateRandom(rand))
                {
                    var cols = dataSet.NumFeatures;
                    var names = new string[cols];
                    for (int i = 0; i < names.Length; ++i)
                        names[i] = string.Format("name{0}", i);
                    dataSet.SetFeatureNames(names);

                    var pms = new Parameters();
                    using (var booster = new Booster(pms, dataSet))
                    {
                        Assert.Equal(1, booster.NumClasses);
                        Assert.Equal(names, booster.FeatureNames);
                    }
                }
        }

        private static MetricType[] _metricTypes = (MetricType[]) Enum.GetValues(typeof(MetricType));
        private MetricType CreateRandomMetric(Random rand)
        {
            return _metricTypes[rand.Next(0, _metricTypes.Length - 1)];
        }

        [Fact]
        public void GetEvalCounts_GetEvalNames()
        {
            var rand = new Random();
            for (int test = 0; test < 100; ++test)
                using (var dataSet = DatasetTest.CreateRandom(rand))
                {
                    var pms = new Parameters();
                    var metric = CreateRandomMetric(rand);
                    if (metric != MetricType.Ndcg && metric != MetricType.Map && (pms.Objective.NumClass > 1 || (metric != MetricType.MultiLogLoss && metric != MetricType.MultiError)))
                    {
                        if (metric == MetricType.Gamma || 
                            metric == MetricType.GammaDeviance ||
                            metric == MetricType.Tweedie || 
                            metric == MetricType.Poisson)
                        {
                            var labels = dataSet.GetLabels();
                            for (var i = 0; i < labels.Length; i++)
                                 labels[i] = (float)Math.Max(1e-3, Math.Abs(labels[i]));
                            dataSet.SetLabels(labels);
                        }

                        pms.Objective.Metric = metric;
                      //pms.Metric.IsProvideTrainingMetric = true;
                        using (var booster = new Booster(pms, dataSet))
                        {
                            var numEval = booster.EvalCounts;
                            var evalNames = booster.EvalNames;
                            Assert.Equal(numEval, evalNames.Length);
                            if (numEval > 0)
                            {
                                Assert.Equal(1, numEval);
                                Assert.Equal(metric, evalNames[0]);
                            }
                        }
                    }
                }
        }


        [Fact]
        public void NumClasses()
        {
            var rand = new Random();
            for (int test = 0; test < 100; ++test)
                using (var dataSet = DatasetTest.CreateRandom(rand))
                {
                    var pms = new Parameters();
                    pms.Objective.Metric = MetricType.MultiLogLoss;
                    pms.Objective.NumClass = rand.Next(2, 4);
                    pms.Objective.Objective = ObjectiveType.MultiClass;
                    using (var booster = new Booster(pms, dataSet))
                    {
                        Assert.Equal(pms.Objective.NumClass, booster.NumClasses);
                    }
                }
        }


        /*
        [Fact]
        public void SaveLoad()
        {
            var rand = new System.Random();
            for (int test = 0; test < 100; ++test)
                using (var ds = DatasetTest.CreateRandom(rand))
                {
                    var pms = new Parameters();
                    using (var booster = new Booster(pms, ds))
                    {
                        var file = System.IO.Path.GetTempFileName();
                        var fileName = file.Substring(0, file.Length - 4) + ".bin";
                        try
                        {
                            booster.SaveModel(0,0,fileName);
                            using (var booster2 = Booster.FromFile(fileName))
                            { 
                                Assert.Equal(booster2.NumFeatures, booster.NumFeatures);
                                Assert.Equal(booster2.NumClasses, booster.NumClasses);
                            }
                        }
                        finally
                        {
                            if (System.IO.File.Exists(fileName))
                                System.IO.File.Delete(fileName);
                        }
                    }
                }
        }
        */
    }
}
