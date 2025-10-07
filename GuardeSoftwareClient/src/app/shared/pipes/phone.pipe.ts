import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'phone',
  standalone: true,
})
export class PhonePipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    // If the value is null or undefined, return an empty string
    if (!value) {
      return '';
    }

    // Clean any non-digit characters
    const cleaned = value.replace(/\D/g, '');

    // Format to international format (e.g., +54 9 11 1234-5678)
    if (cleaned.length === 10) {
      const areaCode = cleaned.substring(0, 2);
      const rest = cleaned.substring(2);
      const formattedRest = `${rest.substring(0, 4)}-${rest.substring(4)}`;
      return `${areaCode} ${formattedRest}`;
    }

    // Format to local format (e.g., 1234-5678)
    if (cleaned.length === 8) {
      return `${cleaned.substring(0, 4)}-${cleaned.substring(4)}`;
    }

    // If the length doesn't match known formats, return the cleaned number
    return cleaned;
  }
}