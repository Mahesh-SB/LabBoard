import { Pipe, PipeTransform } from '@angular/core';
import { AbstractControl } from '@angular/forms';

@Pipe({ name: 'fieldError', standalone: true, pure: false })
export class FieldErrorPipe implements PipeTransform {
  transform(control: AbstractControl | null | undefined): string {
    if (!control?.errors || !control.touched) return '';
    const e = control.errors;
    if (e['required'])    return 'This field is required.';
    if (e['minlength'])   return `Minimum ${e['minlength'].requiredLength} characters required.`;
    if (e['maxlength'])   return `Maximum ${e['maxlength'].requiredLength} characters allowed.`;
    if (e['min'])         return `Minimum value is ${e['min'].min}.`;
    if (e['max'])         return `Maximum value is ${e['max'].max}.`;
    if (e['invalidUrl'])  return 'Enter a valid URL (e.g. https://example.com/callback).';
    if (e['atLeastOne'])  return 'Select at least one option.';
    return 'Invalid value.';
  }
}
