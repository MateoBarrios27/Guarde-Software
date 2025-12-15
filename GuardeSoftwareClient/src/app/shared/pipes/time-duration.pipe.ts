import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'timeduration',
  standalone: true
})
export class TimeDurationPipe implements PipeTransform {

  transform(value: string | Date | undefined): string {
    if (!value) return '';

    const start = new Date(value);
    const end = new Date();

    let months = (end.getFullYear() - start.getFullYear()) * 12;
    months -= start.getMonth();
    months += end.getMonth();

    if (end.getDate() < start.getDate()) {
      months--;
    }

    if (months < 1) {
      return '(1 mes)';
    }

    const years = Math.floor(months / 12);
    const remainingMonths = months % 12;

    const parts = [];

    if (years > 0) {
      parts.push(`${years} ${years === 1 ? 'año' : 'años'}`);
    }

    if (remainingMonths > 0) {
      parts.push(`${remainingMonths} ${remainingMonths === 1 ? 'mes' : 'meses'}`);
    }


    if (parts.length === 0) return '(1 mes)';

    return `(${parts.join(' y ')})`;
  }
}