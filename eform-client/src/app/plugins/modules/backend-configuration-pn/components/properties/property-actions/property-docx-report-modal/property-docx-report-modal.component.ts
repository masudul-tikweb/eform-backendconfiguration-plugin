import {
  Component,
  Inject,
  OnDestroy,
  OnInit,
} from '@angular/core';
import {PropertyAreaModel,} from '../../../../models';
import {BackendConfigurationPnPropertiesService, BackendConfigurationPnReportService} from '../../../../services';
import * as R from 'ramda';
import {saveAs} from 'file-saver';
import {Subscription} from 'rxjs';
import {AutoUnsubscribe} from 'ngx-auto-unsubscribe';
import {ToastrService} from 'ngx-toastr';
import {catchError} from 'rxjs/operators';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';

@AutoUnsubscribe()
@Component({
    selector: 'app-property-docx-report-modal',
    templateUrl: './property-docx-report-modal.component.html',
    styleUrls: ['./property-docx-report-modal.component.scss'],
    standalone: false
})
export class PropertyDocxReportModalComponent implements OnInit, OnDestroy {
  selectedArea: PropertyAreaModel;
  selectedYear: number;
  areasList: PropertyAreaModel[] = [];
  years: number[] = [];

  downloadReportSub$: Subscription;
  getPropertyAreasSub$: Subscription;

  constructor(
    private backendConfigurationPnPropertiesService: BackendConfigurationPnPropertiesService,
    private reportService: BackendConfigurationPnReportService,
    private toasterService: ToastrService,
    public dialogRef: MatDialogRef<PropertyDocxReportModalComponent>,
    @Inject(MAT_DIALOG_DATA) public propertyId: number
  ) {
    this.getPropertyAreasSub$ = this.backendConfigurationPnPropertiesService
      .getPropertyAreas(propertyId)
      .subscribe((data) => {
        if (data && data.success && data.model) {
          this.areasList = data.model.filter(x => x.activated && x.name === '24. IE-indberetning');
        }
      });
  }

  ngOnInit() {
    const currentYear = new Date().getFullYear();
    this.years = R.range(currentYear - 1, currentYear + 10);
  }

  hide() {
    this.dialogRef.close();
  }

  onDownloadReport() {
    this.downloadReportSub$ = this.reportService
      .downloadReport(this.propertyId, this.selectedArea.areaId, this.selectedYear)
      .pipe(catchError((error, caught) => {
        this.toasterService.error('Error downloading report');
        return caught;
      }))
      .subscribe(
        (data) => {
          saveAs(data, this.selectedArea.name + '_' + this.selectedYear + '_report.docx');
        },
      );
  }

  get isDisabledDownloadButton(): boolean {
    return !this.propertyId || !this.selectedArea || !this.selectedYear;
  }

  ngOnDestroy(): void {
  }
}
