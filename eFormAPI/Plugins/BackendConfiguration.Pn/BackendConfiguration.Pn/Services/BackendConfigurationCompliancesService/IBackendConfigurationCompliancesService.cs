﻿/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using BackendConfiguration.Pn.Controllers;
using Microting.eForm.Infrastructure.Models;
using Microting.eFormApi.BasePn.Infrastructure.Models.Application.Case.CaseEdit;

namespace BackendConfiguration.Pn.Services.BackendConfigurationCompliancesService;

using BackendConfiguration.Pn.Infrastructure.Models.Compliances.Index;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.eFormApi.BasePn.Infrastructure.Models.Common;
using System.Threading.Tasks;

public interface IBackendConfigurationCompliancesService
{
    Task<OperationDataResult<Paged<CompliancesModel>>> Index(CompliancesRequestModel request);
    Task<OperationDataResult<int>> ComplianceStatus(int propertyId);
    Task<OperationDataResult<ReplyElement>> Read(int id);
    Task<OperationResult> Update(ReplyRequest model);
    Task<OperationResult> Delete(int id);
    Task<OperationDataResult<CompliancesStatsModel>> Stats();
}