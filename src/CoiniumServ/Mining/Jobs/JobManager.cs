﻿/*
 *   CoiniumServ - crypto currency pool software - https://github.com/CoiniumServ/CoiniumServ
 *   Copyright (C) 2013 - 2014, Coinium Project - http://www.coinium.org
 *
 *   This program is free software: you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation, either version 3 of the License, or
 *   (at your option) any later version.
 *
 *   This program is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU General Public License for more details.
 *
 *   You should have received a copy of the GNU General Public License
 *   along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using Coinium.Coin.Algorithms;
using Coinium.Coin.Daemon;
using Coinium.Miner;
using Coinium.Server.Stratum.Notifications;
using Coinium.Transactions;

namespace Coinium.Mining.Jobs
{
    public class JobManager : IJobManager
    {
        private readonly Dictionary<UInt64, Job> _jobs = new Dictionary<UInt64, Job>();
        private readonly JobCounter _jobCounter = new JobCounter();

        private IExtraNonce _extraNonce;

        private readonly IDaemonClient _daemonClient;

        private readonly IMinerManager _minerManager;

        private readonly IHashAlgorithm _hashAlgorithm;

        public IExtraNonce ExtraNonce { get { return _extraNonce; } }

        public Job LastJob { get; private set; }

        public JobManager(IDaemonClient daemonClient, IMinerManager minerManager, IHashAlgorithm hashAlgorithm)
        {
            _daemonClient = daemonClient;
            _minerManager = minerManager;
            _hashAlgorithm = hashAlgorithm;
        }

        public void Initialize(UInt32 instanceId)
        {
            _extraNonce = new ExtraNonce(instanceId);
        }

        public IJob GetJob(UInt64 id)
        {
            return _jobs.ContainsKey(id) ? _jobs[id] : null;
        }

        public void AddJob(Job job)
        {
            _jobs.Add(job.Id, job);
        }

        /// <summary>
        /// Broadcasts to miners.
        /// </summary>
        /// <example>
        /// sample communication: http://bitcoin.stackexchange.com/a/23112/8899
        /// </example>
        public void Broadcast()
        {
            var blockTemplate = _daemonClient.GetBlockTemplate();
            var generationTransaction = new GenerationTransaction(ExtraNonce, _daemonClient, blockTemplate);
            generationTransaction.Create();

            // create the difficulty notification.
            var difficulty = new Difficulty(16);

            // create the job notification.
            var job = new Job(_jobCounter.Next(), _hashAlgorithm, blockTemplate, generationTransaction)
            {
                CleanJobs = true // tell the miners to clean their existing jobs and start working on new one.
            };

            _jobs.Add(job.Id,job);
            LastJob = job;

            foreach (var miner in _minerManager.GetAll())
            {
                if (!miner.SupportsJobNotifications)
                    continue;

                miner.SendDifficulty(difficulty);
                miner.SendJob(job);
            }
        }
    }
}
