import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map, of, throwError } from 'rxjs';
import { environment } from '../../../../environments/environments';
import {
  CommunicationTemplate,
  CommunicationTemplateList,
  SaveCommunicationTemplateRequest
} from '../../models/communication-template';
import { getSystemTemplate } from '../../../shared/utils/communication-template-catalog';

@Injectable({ providedIn: 'root' })
export class CommunicationTemplateService {
  private readonly url = `${environment.apiUrl}/CommunicationTemplates`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<CommunicationTemplateList> {
    return this.http.get<CommunicationTemplateList>(this.url);
  }

  create(request: SaveCommunicationTemplateRequest): Observable<CommunicationTemplate> {
    return this.http.post<CommunicationTemplate>(this.url, request);
  }

  update(id: number, request: SaveCommunicationTemplateRequest): Observable<CommunicationTemplate> {
    return this.http.put<CommunicationTemplate>(`${this.url}/${id}`, request);
  }

  reorder(templateIds: number[]): Observable<CommunicationTemplateList> {
    return this.http.put<CommunicationTemplateList>(`${this.url}/order`, { templateIds });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }

  /** Original del sistema (sin las ediciones guardadas en la base). */
  resolveOriginalContent(sourceKey: string): Observable<string> {
    const definition = getSystemTemplate(sourceKey);
    if (!definition) {
      return throwError(() => new Error(`Plantilla del sistema desconocida: ${sourceKey}`));
    }
    if (definition.inlineContent !== undefined) {
      return of(definition.inlineContent);
    }
    return this.http.get(definition.assetUrl!, { responseType: 'text' }).pipe(
      map(html => {
        if (definition.isValid && !definition.isValid(html)) {
          throw new Error(`El archivo de la plantilla ${definition.name} no es válido.`);
        }
        return html;
      })
    );
  }

  /** Contenido que se carga en un comunicado: el editado si existe; si no, el original del sistema. */
  resolveContent(template: CommunicationTemplate): Observable<string> {
    if (template.content !== null && template.content !== undefined) {
      return of(template.content);
    }
    if (template.sourceKey) {
      return this.resolveOriginalContent(template.sourceKey);
    }
    return of('');
  }
}
