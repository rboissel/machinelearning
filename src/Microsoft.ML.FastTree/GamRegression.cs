﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.ML.Core.Data;
using Microsoft.ML.Data;
using Microsoft.ML.Runtime;
using Microsoft.ML.Runtime.CommandLine;
using Microsoft.ML.Runtime.Data;
using Microsoft.ML.Runtime.Internal.Internallearn;
using Microsoft.ML.Runtime.Model;
using Microsoft.ML.Runtime.Training;
using Microsoft.ML.Trainers.FastTree;
using Microsoft.ML.Trainers.FastTree.Internal;
using System;

[assembly: LoadableClass(RegressionGamTrainer.Summary,
    typeof(RegressionGamTrainer), typeof(RegressionGamTrainer.Arguments),
    new[] { typeof(SignatureRegressorTrainer), typeof(SignatureTrainer), typeof(SignatureFeatureScorerTrainer) },
    RegressionGamTrainer.UserNameValue,
    RegressionGamTrainer.LoadNameValue,
    RegressionGamTrainer.ShortName, DocName = "trainer/GAM.md")]

[assembly: LoadableClass(typeof(RegressionGamPredictor), null, typeof(SignatureLoadModel),
    "GAM Regression Predictor",
    RegressionGamPredictor.LoaderSignature)]

namespace Microsoft.ML.Trainers.FastTree
{
    public sealed class RegressionGamTrainer : GamTrainerBase<RegressionGamTrainer.Arguments, RegressionPredictionTransformer<RegressionGamPredictor>, RegressionGamPredictor>
    {
        public partial class Arguments : ArgumentsBase
        {
            [Argument(ArgumentType.AtMostOnce, HelpText = "Metric for pruning. (For regression, 1: L1, 2:L2; default L2)", ShortName = "pmetric")]
            [TGUI(Description = "Metric for pruning. (For regression, 1: L1, 2:L2; default L2")]
            public int PruningMetrics = 2;
        }

        internal const string LoadNameValue = "RegressionGamTrainer";
        internal const string UserNameValue = "Generalized Additive Model for Regression";
        internal const string ShortName = "gamr";

        public override PredictionKind PredictionKind => PredictionKind.Regression;

        internal RegressionGamTrainer(IHostEnvironment env, Arguments args)
             : base(env, args, LoadNameValue, TrainerUtils.MakeR4ScalarLabel(args.LabelColumn)) { }

        /// <summary>
        /// Initializes a new instance of <see cref="FastTreeBinaryClassificationTrainer"/>
        /// </summary>
        /// <param name="env">The private instance of <see cref="IHostEnvironment"/>.</param>
        /// <param name="labelColumn">The name of the label column.</param>
        /// <param name="featureColumn">The name of the feature column.</param>
        /// <param name="weightColumn">The name for the column containing the initial weight.</param>
        /// <param name="numIterations">The number of iterations to use in learning the features.</param>
        /// <param name="learningRate">The learning rate. GAMs work best with a small learning rate.</param>
        /// <param name="maxBins">The maximum number of bins to use to approximate features</param>
        /// <param name="advancedSettings">A delegate to apply all the advanced arguments to the algorithm.</param>
        public RegressionGamTrainer(IHostEnvironment env,
            string labelColumn = DefaultColumnNames.Label,
            string featureColumn = DefaultColumnNames.Features,
            string weightColumn = null,
            int numIterations = GamDefaults.NumIterations,
            double learningRate = GamDefaults.LearningRates,
            int maxBins = GamDefaults.MaxBins,
            Action<Arguments> advancedSettings = null)
            : base(env, LoadNameValue, TrainerUtils.MakeR4ScalarLabel(labelColumn), featureColumn, weightColumn, numIterations, learningRate, maxBins, advancedSettings)
        {
        }

        internal override void CheckLabel(RoleMappedData data)
        {
            data.CheckRegressionLabel();
        }

        private protected override RegressionGamPredictor TrainModelCore(TrainContext context)
        {
            TrainBase(context);
            return new RegressionGamPredictor(Host, InputLength, TrainSet, MeanEffect, BinEffects, FeatureMap);
        }

        protected override ObjectiveFunctionBase CreateObjectiveFunction()
        {
            return new FastTreeRegressionTrainer.ObjectiveImpl(TrainSet, Args);
        }

        protected override void DefinePruningTest()
        {
            var validTest = new RegressionTest(ValidSetScore, Args.PruningMetrics);
            // Because we specify pruning metrics as L2 by default, the results array will have 1 value
            PruningLossIndex = 0;
            PruningTest = new TestHistory(validTest, PruningLossIndex);
        }

        protected override RegressionPredictionTransformer<RegressionGamPredictor> MakeTransformer(RegressionGamPredictor model, Schema trainSchema)
            => new RegressionPredictionTransformer<RegressionGamPredictor>(Host, model, trainSchema, FeatureColumn.Name);

        public RegressionPredictionTransformer<RegressionGamPredictor> Train(IDataView trainData, IDataView validationData = null)
            => TrainTransformer(trainData, validationData);

        protected override SchemaShape.Column[] GetOutputColumnsCore(SchemaShape inputSchema)
        {
            return new[]
            {
                new SchemaShape.Column(DefaultColumnNames.Score, SchemaShape.Column.VectorKind.Scalar, NumberType.R4, false, new SchemaShape(MetadataUtils.GetTrainerOutputMetadata()))
            };
        }
    }

    public class RegressionGamPredictor : GamPredictorBase
    {
        public const string LoaderSignature = "RegressionGamPredictor";
        public override PredictionKind PredictionKind => PredictionKind.Regression;

        public RegressionGamPredictor(IHostEnvironment env, int inputLength, Dataset trainset,
            double meanEffect, double[][] binEffects, int[] featureMap)
            : base(env, LoaderSignature, inputLength, trainset, meanEffect, binEffects, featureMap) { }

        private RegressionGamPredictor(IHostEnvironment env, ModelLoadContext ctx)
            : base(env, LoaderSignature, ctx) { }

        public static VersionInfo GetVersionInfo()
        {
            return new VersionInfo(
                modelSignature: "GAM REGP",
                verWrittenCur: 0x00010001,
                verReadableCur: 0x00010001,
                verWeCanReadBack: 0x00010001,
                loaderSignature: LoaderSignature,
                loaderAssemblyName: typeof(RegressionGamPredictor).Assembly.FullName);
        }

        public static RegressionGamPredictor Create(IHostEnvironment env, ModelLoadContext ctx)
        {
            Contracts.CheckValue(env, nameof(env));
            env.CheckValue(ctx, nameof(ctx));
            ctx.CheckAtModel(GetVersionInfo());

            return new RegressionGamPredictor(env, ctx);
        }

        public override void Save(ModelSaveContext ctx)
        {
            Host.CheckValue(ctx, nameof(ctx));
            ctx.CheckAtModel();
            ctx.SetVersionInfo(GetVersionInfo());
            base.Save(ctx);
        }
    }
}
