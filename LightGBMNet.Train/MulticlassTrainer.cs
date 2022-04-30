﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using LightGBMNet.Tree;

namespace LightGBMNet.Train
{
    public class MulticlassNativePredictor : NativePredictorBase<double []>
    {
        public override PredictionKind PredictionKind => PredictionKind.MultiClassClassification;

        public MulticlassNativePredictor(Booster booster) : base(booster)
        {
        }

        private protected override double [] ConvertOutput(double[] output)
        {
            return output;
        }

        public override double[][] GetOutputs(float[][] rows, int startIteration, int numIterations)
        {
            var output = Booster.PredictForMatsMulti(Booster.PredictType.Normal, rows, startIteration, (numIterations == -1) ? MaxNumTrees : numIterations);

            // convert from 2D array to array of rows
            var numRows = output.GetLength(0);
            var numCols = output.GetLength(1);
            var rslt = new double[numRows][];
            for (int i=0; i<rslt.Length; i++)
            {
                var row = new double[numCols];
                for (int j = 0; j < row.Length; j++)
                    row[j] = output[i, j];
                rslt[i] = row;
            }
            return rslt;
        }

    }

    public sealed class MulticlassTrainer : TrainerBase<double []>
    {
        public override PredictionKind PredictionKind => PredictionKind.MultiClassClassification;

        public MulticlassTrainer(LearningParameters lp, ObjectiveParameters op) : base(lp, op)
        {
            if(!(op.Objective == ObjectiveType.MultiClass || op.Objective == ObjectiveType.MultiClassOva))
                throw new Exception("Require Objective == MultiClass or MultiClassOva");

            if (op.NumClass <= 1)
                throw new Exception("Require NumClass > 1");

            if (op.Metric == MetricType.DefaultMetric)
                op.Metric = MetricType.MultiLogLoss;     // TODO: why was this MultiError?????
        }

        public MulticlassTrainer( LearningParameters lp
                                , ObjectiveParameters op
                                , IVectorisedPredictorWithFeatureWeights<double> nativePredictor
                                , Datasets datasets
                                ) : base(lp, op)
        {
            if (!(op.Objective == ObjectiveType.MultiClass || op.Objective == ObjectiveType.MultiClassOva))
                throw new Exception("Require Objective == MultiClass or MultiClassOva");

            if (op.NumClass <= 1)
                throw new Exception("Require NumClass > 1");

            if (op.Metric == MetricType.DefaultMetric)
                op.Metric = MetricType.MultiLogLoss;     // TODO: why was this MultiError?????

            if (nativePredictor == null)
                throw new Exception("nativePredictor is null");
            if (datasets == null)
                throw new Exception("datasets is null");
            // this is because there is no equivalent of Booster.ResetTrainingData for validation data
            if (datasets.Validation != null)
                throw new Exception("Not supported: new validation dataset for existing booster. Please set dataset.Validation to null.");
            Datasets = datasets;
            if (nativePredictor is MulticlassNativePredictor b)
            {
                Booster = b.Booster.Clone();
                Booster.ResetTrainingData(datasets.Training);
            }
            else
                throw new Exception("nativePredictor is not a multiclass predictor");
        }

        private Ensemble GetBinaryEnsemble(int classID)
        {
            var numClass = Objective.NumClass;
            Ensemble res = new Ensemble();
            for (int i = classID; i < TrainedEnsemble.NumTrees; i += numClass)
            {
                res.AddTree(TrainedEnsemble.GetTreeAt(i));
            }
            return res;
        }

        private BinaryPredictor CreateBinaryPredictor(int classID)
        {
            return new BinaryPredictor(GetBinaryEnsemble(classID), FeatureCount, AverageOutput);
        }

        private protected override IPredictorWithFeatureWeights<double []> CreateManagedPredictor()
        {
            var numClass = Objective.NumClass;
            if (TrainedEnsemble.NumTrees % numClass != 0)
                throw new Exception("Number of trees should be a multiple of number of classes.");

            var isSoftMax = (Objective.Objective == ObjectiveType.MultiClass);
            IPredictorWithFeatureWeights<double>[] predictors = new IPredictorWithFeatureWeights<double>[numClass];
            var cali = isSoftMax ? null : new PlattCalibrator(-Objective.Sigmoid);
            for (int i = 0; i < numClass; ++i)
            {
                var pred = CreateBinaryPredictor(i) as IPredictorWithFeatureWeights<double>;
                predictors[i] = isSoftMax ? pred : new CalibratedPredictor(pred, cali);
            }
            return OvaPredictor.Create(isSoftMax, predictors);
        }

        private protected override IVectorisedPredictorWithFeatureWeights<double []> CreateNativePredictor()
        {
            return new MulticlassNativePredictor(Booster.Clone());
        }
    }

}
