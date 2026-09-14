import { AfterViewInit, Component, ElementRef, EventEmitter, Input, OnDestroy, Output, ViewChild } from '@angular/core';
import { IconComponent } from './icon.component';
@Component({selector:'app-dialog',standalone:true,imports:[IconComponent],template:`
 <dialog #dialog class="modal-box" [class.wide]="wide" aria-labelledby="dialog-title" (cancel)="cancel($event)">
 <header class="modal-head"><div><span class="eyebrow">{{eyebrow}}</span><h2 id="dialog-title">{{title}}</h2></div>
 <button class="icon-button" aria-label="Cerrar ventana" type="button" (click)="dismiss.emit()"><app-icon name="close"/></button></header>
 <ng-content/>
 </dialog>`})
export class DialogComponent implements AfterViewInit,OnDestroy {
 @Input() title=''; @Input() eyebrow='FLORÉ · DEMO'; @Input() wide=false;
 @Output() dismiss=new EventEmitter<void>(); @ViewChild('dialog') dialog!:ElementRef<HTMLDialogElement>;
 private previous:HTMLElement|null=null; private overflow='';
 ngAfterViewInit(){this.previous=document.activeElement as HTMLElement;this.overflow=document.body.style.overflow;document.body.style.overflow='hidden';this.dialog.nativeElement.showModal();}
 cancel(e:Event){e.preventDefault();this.dismiss.emit();}
 ngOnDestroy(){this.dialog.nativeElement.close();document.body.style.overflow=this.overflow;this.previous?.focus();}
}
