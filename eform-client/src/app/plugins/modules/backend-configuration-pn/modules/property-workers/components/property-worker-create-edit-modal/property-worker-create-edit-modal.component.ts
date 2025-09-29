import {
  Component,
  EventEmitter,
  Inject,
  OnDestroy,
  OnInit, Output,
} from '@angular/core';
import {AutoUnsubscribe} from 'ngx-auto-unsubscribe';
import {Subscription} from 'rxjs';
import {CommonDictionaryModel, LanguagesModel} from 'src/app/common/models';
import {PropertyAssignmentWorkerModel, DeviceUserModel} from '../../../../models';
import {BackendConfigurationPnPropertiesService} from '../../../../services';
import {AuthStateService} from 'src/app/common/store';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';
import {MtxGridColumn} from '@ng-matero/extensions/grid';
import {TranslateService} from '@ngx-translate/core';
import {tap} from 'rxjs/operators';
import {AppSettingsStateService} from 'src/app/modules/application-settings/components/store';
import {FormBuilder, FormGroup, Validators} from '@angular/forms';
import validator from 'validator';

@AutoUnsubscribe()
@Component({
    selector: 'app-property-worker-create-edit-modal',
    templateUrl: './property-worker-create-edit-modal.component.html',
    styleUrls: ['./property-worker-create-edit-modal.component.scss'],
    standalone: false
})
export class PropertyWorkerCreateEditModalComponent implements OnInit, OnDestroy {
  availableProperties: CommonDictionaryModel[] = [];
  edit: boolean = false;
  selectedDeviceUser: DeviceUserModel = new DeviceUserModel();
  selectedDeviceUserCopy: DeviceUserModel = new DeviceUserModel();
  assignments: PropertyAssignmentWorkerModel[] = [];
  assignmentsCopy: PropertyAssignmentWorkerModel[] = [];
  taskManagementEnabled: boolean = false;
  timeRegistrationEnabled: boolean = false;
  @Output() userUpdated: EventEmitter<void> = new EventEmitter<void>();
  tableHeaders: MtxGridColumn[] = [
    {
      header: this.translateService.stream('ID'),
      field: 'id',
    },
    {
      header: this.translateService.stream('Property name'),
      field: 'name',
      class: 'propertyName',
    },
    {
      header: this.translateService.stream('Select'),
      field: 'select',
    },
  ];

  deviceUserCreate$: Subscription;
  deviceUserUpdate$: Subscription;
  deviceUserAssign$: Subscription;
  getLanguagesSub$: Subscription;
  appLanguages: LanguagesModel = new LanguagesModel();
  activeLanguages: Array<any> = [];
  form: FormGroup;

  constructor(
    private fb: FormBuilder,
    public propertiesService: BackendConfigurationPnPropertiesService,
    public authStateService: AuthStateService,
    private translateService: TranslateService,
    public dialogRef: MatDialogRef<PropertyWorkerCreateEditModalComponent>,
    private appSettingsStateService: AppSettingsStateService,
    @Inject(MAT_DIALOG_DATA) model:
    {
      deviceUser: DeviceUserModel,
      assignments: PropertyAssignmentWorkerModel[],
      availableProperties: CommonDictionaryModel[],
    },
  ) {
    this.assignments = [...model.assignments];
    this.availableProperties = [...model.availableProperties];
    this.selectedDeviceUser = {...model.deviceUser ?? new DeviceUserModel()};
    this.selectedDeviceUserCopy = {...model.deviceUser};
    this.assignmentsCopy = [...model.assignments];
    this.taskManagementEnabled = this.selectedDeviceUserCopy.taskManagementEnabled;
    this.timeRegistrationEnabled = this.selectedDeviceUserCopy.timeRegistrationEnabled;

    this.form = this.fb.group({
      userFirstName: [this.selectedDeviceUser.userFirstName || '', Validators.required],
      userLastName: [this.selectedDeviceUser.userLastName || '', Validators.required],
      workerEmail: [this.selectedDeviceUser.workerEmail || '', [
        Validators.required,
        (control) => validator.isEmail(control.value) ? null : {invalidEmail: true}
      ]],
      phoneNumber: [this.selectedDeviceUser.phoneNumber || '', [
        (control) => {
          const value = control.value;
          if (!value) {
            return null;
          }
          return validator.isMobilePhone(value) ? null : {invalidPhoneNumber: true};
        }
      ]],
      pinCode: [this.selectedDeviceUser.pinCode || ''],
      employeeNo: [this.selectedDeviceUser.employeeNo || ''],
      languageCode: [this.selectedDeviceUser.languageCode || ''],
      timeRegistrationEnabled: [this.selectedDeviceUser.timeRegistrationEnabled || false],
      taskManagementEnabled: [this.selectedDeviceUser.taskManagementEnabled || false],
    });
  }

  get languages() {
    return this.appLanguages.languages.filter((x) => x.isActive);
  }

  // Add this method to your component
  updateFormControlDisabledStates() {
    // userFirstName and userLastName
    // if (this.selectedDeviceUser.isBackendUser) {
    //   this.form.get('userFirstName')?.disable();
    //   this.form.get('userLastName')?.disable();
    // } else {
    //   this.form.get('userFirstName')?.enable();
    //   this.form.get('userLastName')?.enable();
    // }

    // languageCode
    const shouldDisableLanguage =
      this.timeRegistrationEnabled ||
      this.taskManagementEnabled ||
      this.getAssignmentCount() > 0;
    if (shouldDisableLanguage) {
      this.form.get('languageCode')?.disable();
    } else {
      this.form.get('languageCode')?.enable();
    }

    // taskManagementEnabled (mat-slide-toggle)
    if (this.selectedDeviceUser.hasWorkOrdersAssigned) {
      this.form.get('taskManagementEnabled')?.disable();
    } else {
      this.form.get('taskManagementEnabled')?.enable();
    }
  }

  ngOnInit() {
    this.getEnabledLanguages();
    this.updateFormControlDisabledStates();
  }

  hide(result = false) {
    this.selectedDeviceUser = new DeviceUserModel();
    this.assignments = [];
    this.dialogRef.close(result);
  }

  addToArray(e: any, propertyId: number) {
    const assignmentObject = new PropertyAssignmentWorkerModel();
    if (e.checked) {
      assignmentObject.isChecked = true;
      assignmentObject.propertyId = propertyId;
      this.assignments = [...this.assignments, assignmentObject];
    } else {
      this.assignments = this.assignments.filter(
        (x) => x.propertyId !== propertyId
      );
    }
  }

  getAssignmentIsCheckedByPropertyId(propertyId: number): boolean {
    const assignment = this.assignments.find(
      (x) => x.propertyId === propertyId
    );
    return assignment ? assignment.isChecked : false;
  }

  getAssignmentIsLockedByPropertyId(propertyId: number): boolean {
    const assignment = this.assignments.find(
      (x) => x.propertyId === propertyId
    );
    return assignment ? assignment.isLocked : false;
  }

  updateSingle() {
    if (this.form.invalid) {
      return;
    }
    const formValue = this.form.value;
    Object.assign(this.selectedDeviceUser, formValue);
    this.selectedDeviceUser.siteUid = this.selectedDeviceUser.id;
    this.deviceUserCreate$ = this.propertiesService
      .updateSingleDeviceUser(this.selectedDeviceUser)
      .subscribe((operation) => {
        if (operation && operation.success && this.assignments) {
          this.assignWorkerToPropertiesUpdate();
        } else {
          this.hide(true);
        }
      });
  }

  createDeviceUser() {
    if (this.form.invalid) {
      return;
    }
    const formValue = this.form.value;
    Object.assign(this.selectedDeviceUser, formValue);
    this.deviceUserCreate$ = this.propertiesService
      .createSingleDeviceUser(this.selectedDeviceUser)
      .subscribe((operation) => {
        if (operation && operation.success) {
          if (this.assignments && this.assignments.length > 0) {
            this.assignWorkerToProperties(operation.model);
          } else {
            this.hide(true);
          }
        }
      });
  }

  assignWorkerToProperties(siteId: number) {
    this.deviceUserAssign$ = this.propertiesService
      .assignPropertiesToWorker({
        siteId,
        assignments: this.assignments,
        // eslint-disable-next-line max-len
        timeRegistrationEnabled: this.form.value.timeRegistrationEnabled === undefined ? this.selectedDeviceUser.timeRegistrationEnabled : this.form.value.timeRegistrationEnabled,
        // eslint-disable-next-line max-len
        taskManagementEnabled: this.form.value.taskManagementEnabled === undefined ? this.selectedDeviceUser.taskManagementEnabled : this.form.value.taskManagementEnabled,
      })
      .subscribe((operation) => {
        if (operation && operation.success) {
          this.hide(true);
        }
      });
  }

  assignWorkerToPropertiesUpdate() {
    this.deviceUserAssign$ = this.propertiesService
      .updateAssignPropertiesToWorker({
        siteId: this.selectedDeviceUser.normalId,
        assignments: this.assignments,
        // eslint-disable-next-line max-len
        timeRegistrationEnabled: this.form.value.timeRegistrationEnabled === undefined ? this.selectedDeviceUser.timeRegistrationEnabled : this.form.value.timeRegistrationEnabled,
        // eslint-disable-next-line max-len
        taskManagementEnabled: this.form.value.taskManagementEnabled === undefined ? this.selectedDeviceUser.taskManagementEnabled : this.form.value.taskManagementEnabled,
      })
      .subscribe((operation) => {
        if (operation && operation.success) {
          this.hide(true);
        }
      });
  }

  getAssignmentByPropertyId(propertyId: number): PropertyAssignmentWorkerModel {
    return (
      this.assignments.find((x) => x.propertyId === propertyId) ?? {
        propertyId: propertyId,
        isChecked: false,
        isLocked: false,
      }
    );
  }

  getAssignmentCount(): number {
    return this.assignmentsCopy.filter((x) => x.isChecked).length;
  }

  ngOnDestroy(): void {
  }

  getEnabledLanguages() {
    this.getLanguagesSub$ = this.appSettingsStateService.getLanguages()
      .pipe(tap(data => {
        if (data && data.success && data.model) {
          this.appLanguages = data.model;
          this.activeLanguages = this.appLanguages.languages.filter((x) => x.isActive);
          if (this.selectedDeviceUser.id) {
            this.edit = true;
          }
          if (!this.edit) {
            this.form.patchValue({languageCode: this.languages[0].languageCode});
            if (this.authStateService.checkClaim('task_management_enable')) {
              this.form.patchValue({taskManagementEnabled: false});
            }
          }
        }
      }))
      .subscribe();
  }
}
