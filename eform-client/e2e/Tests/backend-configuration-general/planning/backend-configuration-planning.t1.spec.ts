import loginPage from '../../../Page objects/Login.page';
import backendConfigurationPropertiesPage, {
  PropertyCreateUpdate,
} from '../../../Page objects/BackendConfiguration/BackendConfigurationProperties.page';
import { expect } from 'chai';
import { generateRandmString } from '../../../Helpers/helper-functions';
import backendConfigurationPropertyWorkersPage from '../../../Page objects/BackendConfiguration/BackendConfigurationPropertyWorkers.page';
import backendConfigurationAreaRulesPage, {
  AreaRuleCreateUpdate,
  AreaRulePlanningCreateUpdate,
} from '../../../Page objects/BackendConfiguration/BackendConfigurationAreaRules.page';
import { format } from 'date-fns';
import itemsPlanningPlanningPage from '../../../Page objects/ItemsPlanning/ItemsPlanningPlanningPage';
import { $ } from '@wdio/globals';

const property: PropertyCreateUpdate = {
  name: generateRandmString(),
  chrNumber: generateRandmString(),
  address: generateRandmString(),
  cvrNumber: '1111111',
  // selectedLanguages: [{ languageId: 1, languageName: 'Dansk' }],
};
const workerForCreate = {
  name: generateRandmString(),
  surname: generateRandmString(),
  language: 'Dansk',
  properties: [0],
  workerEmail: generateRandmString() + '@test.com',
};
const areaRuleForCreate: AreaRuleCreateUpdate = {
  name: generateRandmString(),
  eform: 'Kontrol flydelag',
};

describe('Backend Configuration Area Rules Planning Type1', function () {
  before(async () => {
    await loginPage.open('/auth');
    await loginPage.login();
    await backendConfigurationPropertiesPage.goToProperties();
    await backendConfigurationPropertiesPage.createProperty(property);
    await backendConfigurationPropertyWorkersPage.goToPropertyWorkers();
    await backendConfigurationPropertyWorkersPage.create(workerForCreate);
    await backendConfigurationPropertiesPage.goToProperties();
    let lastProperty = await backendConfigurationPropertiesPage.getLastPropertyRowObject();
    await lastProperty.editBindWithAreas([0]); // bind specific type1
    lastProperty = await backendConfigurationPropertiesPage.getLastPropertyRowObject();
    await lastProperty.openAreasViewModal(0); // go to area rule page
  });
  it('should create new planning from default area rule', async () => {
    const rowNum = await backendConfigurationAreaRulesPage.rowNum();
    expect(rowNum, 'have some non-default area rules').eq(0);
    await backendConfigurationAreaRulesPage.createAreaRule(areaRuleForCreate);
    expect(rowNum + 1).eq(await backendConfigurationAreaRulesPage.rowNum());
    const areRule = await backendConfigurationAreaRulesPage.getLastAreaRuleRowObject();
    expect(areRule.name).eq(areaRuleForCreate.name);
    expect(areRule.eform).eq('Kontrol flydelag');
    expect(areRule.rulePlanningStatus).eq(false);
    const areaRule = await backendConfigurationAreaRulesPage.getFirstAreaRuleRowObject();
    const areaRulePlanning: AreaRulePlanningCreateUpdate = {
    //   startDate: format(new Date(), 'yyyy/MM/dd'),
      workers: [{ workerNumber: 0 }],
      enableCompliance: true,
    };
    await areaRule.createUpdatePlanning(areaRulePlanning);
    // areaRulePlanning.startDate = format(
    //   sub(new Date(), { days: 1 }),
    //   'yyyy/MM/dd'
    // ); // fix test
    const areaRulePlanningCreated = await areaRule.readPlanning();
    // expect(areaRulePlanningCreated.startDate).eq(areaRulePlanning.startDate);
    expect(areaRulePlanningCreated.workers[0].name).eq(
      `${workerForCreate.name} ${workerForCreate.surname}`
    );
    // expect(
    //   await (await $(`#mat-checkbox-0`)).getValue(),
    //   `User ${areaRulePlanningCreated.workers[0]} not paired`
    // ).eq('true');
    expect(areaRulePlanningCreated.workers[0].checked).eq(true);
    expect(areaRulePlanningCreated.workers[0].status).eq('Klar til server');
    expect(areaRulePlanningCreated.enableCompliance).eq(areaRulePlanning.enableCompliance);
    await itemsPlanningPlanningPage.goToPlanningsPage();
    expect(
      await itemsPlanningPlanningPage.rowNum(),
      'items planning not create or create not correct'
    ).eq(1);
    const itemPlanning = await itemsPlanningPlanningPage.getLastPlanningRowObject(true);
    expect(itemPlanning.eFormName).eq('Kontrol flydelag');
    expect(itemPlanning.name).eq(areaRule.name);
    expect(itemPlanning.folderName).eq(
      `${property.name} - 00. Logbøger`
    );
    expect(itemPlanning.repeatEvery).eq(12);
    expect(itemPlanning.repeatType).eq('Måned');
    const workers = await itemPlanning.readPairing();
    expect([
      {
        workerName: `${workerForCreate.name} ${workerForCreate.surname}`,
        workerValue: true,
      },
    ]).deep.eq(workers);
    // browser.back();
    // await areaRule.createUpdatePlanning({status: false});
  });
  after(async () => {
    await backendConfigurationPropertiesPage.goToProperties();
    await backendConfigurationPropertiesPage.clearTable();
    await backendConfigurationPropertyWorkersPage.goToPropertyWorkers();
    await backendConfigurationPropertyWorkersPage.clearTable();
  });
});
