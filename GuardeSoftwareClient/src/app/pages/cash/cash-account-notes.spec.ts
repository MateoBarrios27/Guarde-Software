import { of, throwError } from 'rxjs';
import { CashComponent } from './cash.component';
import { FinancialAccount } from '../../core/models/cash';
import Swal from '../../shared/services/ui-alert.service';

// Notas (comentarios) de las cuentas de Caja y cambio de mes. Se instancia la clase real con servicios simulados; no se
// toca el DOM (el popup es un overlay y se comprueba en el navegador).
function account(id: number, name: string, comment: string | null = null): FinancialAccount {
  return { id, name, type: 'Banco', currency: 'ARS', balance: 1000, displayOrder: id, color: '#1f2937', comment, commentUpdatedAt: null };
}

describe('Caja: notas de las cuentas', () => {
  let cashService: { updateAccountComment: jasmine.Spy };
  let component: CashComponent;
  let galicia: FinancialAccount;
  let efectivo: FinancialAccount;

  beforeEach(() => {
    sessionStorage.clear();
    cashService = {
      updateAccountComment: jasmine.createSpy('updateAccountComment').and.returnValue(of({ commentUpdatedAt: '2026-11-10T10:00:00' })),
    };
    const deleteConfirmation = { confirm: jasmine.createSpy('confirm').and.resolveTo(true) };
    const router = { url: '/cash' };
    component = new CashComponent(cashService as never, {} as never, deleteConfirmation as never, router as never);
    galicia = account(1, 'Banco Galicia', 'Transferir el 5');
    efectivo = account(2, 'Caja Fuerte');
    component.accounts = [galicia, efectivo];
    spyOn(Swal, 'fire').and.resolveTo({} as never);
  });

  it('una cuenta con nota se reconoce; una sin nota, o sólo con espacios, no', () => {
    expect(component.hasAccountComment(galicia)).toBeTrue();
    expect(component.hasAccountComment(efectivo)).toBeFalse();
    efectivo.comment = '   ';
    expect(component.hasAccountComment(efectivo)).toBeFalse();
  });

  it('abrir y cerrar sin escribir no guarda nada', () => {
    component.toggleAccountComment(galicia);
    expect(component.activeCommentAccount).toBe(galicia);
    expect(component.isAccountCommentPinned).toBeTrue();

    component.closeAccountComment(galicia);

    expect(component.activeCommentAccount).toBeNull();
    expect(cashService.updateAccountComment).not.toHaveBeenCalled();
    expect(component.undoStack.length).toBe(0);
  });

  it('escribir una nota y cerrar la guarda una vez, sin espacios de más, con la fecha del servidor', () => {
    component.toggleAccountComment(efectivo);
    component.captureAccount(efectivo); // el texto toma el foco
    efectivo.comment = '  Contar los viernes  ';

    component.closeAccountComment(efectivo);

    expect(cashService.updateAccountComment).toHaveBeenCalledOnceWith(2, 'Contar los viernes');
    expect(efectivo.comment).toBe('Contar los viernes');
    expect(efectivo.commentUpdatedAt).toBe('2026-11-10T10:00:00');
  });

  it('el cambio queda en el historial para deshacer, con la nota anterior', () => {
    component.toggleAccountComment(galicia);
    component.captureAccount(galicia);
    galicia.comment = 'Transferir el 6';

    component.closeAccountComment(galicia);

    expect(component.undoStack.length).toBe(1);
    const action = component.undoStack[0];
    expect(action.type).toBe('ACCOUNT_EDIT');
    expect(action.targetId).toBe(1);
    expect(action.oldState.comment).toBe('Transferir el 5');
    expect(action.newState.comment).toBe('Transferir el 6');
  });

  it('si el servidor falla, vuelve a la nota guardada y avisa', () => {
    cashService.updateAccountComment.and.returnValue(throwError(() => new Error('500')));
    component.toggleAccountComment(galicia);
    galicia.comment = 'Texto que no se guarda';

    component.closeAccountComment(galicia);

    expect(galicia.comment).toBe('Transferir el 5');
    expect(Swal.fire).toHaveBeenCalledWith('Error', 'No se pudo guardar la nota de la cuenta.', 'error');
  });

  it('un solo popup de nota a la vez: abrir otra cuenta cierra y guarda la anterior', () => {
    component.toggleAccountComment(galicia);
    galicia.comment = 'Nota nueva';

    component.toggleAccountComment(efectivo);

    expect(component.activeCommentAccount).toBe(efectivo);
    expect(cashService.updateAccountComment).toHaveBeenCalledOnceWith(1, 'Nota nueva');
  });

  it('borrar la nota la vacía en el servidor y la deja en el historial', async () => {
    await component.deleteAccountComment(galicia);

    expect(galicia.comment).toBe('');
    expect(cashService.updateAccountComment).toHaveBeenCalledOnceWith(1, '');
    expect(component.undoStack.length).toBe(1);
    expect(component.undoStack[0].oldState.comment).toBe('Transferir el 5');
  });

  it('el estado capturado de otra cuenta no se mezcla con el de ésta', () => {
    // Se enfocó un campo de Banco Galicia y, sin salir de él, se cierra la nota de Caja Fuerte.
    component.captureAccount(galicia);
    efectivo.comment = 'Algo';

    component.checkAccountChange(efectivo);

    expect(component.undoStack.length).toBe(0);
  });

  it('con Shift+Enter no cierra la nota; con Enter sí', () => {
    component.toggleAccountComment(efectivo);

    const shiftEnter = { shiftKey: true, preventDefault: jasmine.createSpy('preventDefault') } as unknown as Event;
    component.onAccountCommentEnter(shiftEnter, efectivo);
    expect(component.activeCommentAccount).toBe(efectivo);
    expect(shiftEnter.preventDefault).not.toHaveBeenCalled();

    const enter = { shiftKey: false, preventDefault: jasmine.createSpy('preventDefault') } as unknown as Event;
    component.onAccountCommentEnter(enter, efectivo);
    expect(component.activeCommentAccount).toBeNull();
    expect(enter.preventDefault).toHaveBeenCalled();
  });

  it('elegir el mes que ya se está viendo no pide confirmación ni cambia nada', () => {
    component.selectedMonth = 11;
    component.selectedYear = 2026;

    component.goToMonth(11, 2026);

    expect(Swal.fire).not.toHaveBeenCalled();
  });

  it('no deja ir a un año anterior a 2026', () => {
    component.selectedMonth = 1;
    component.selectedYear = 2026;

    component.goToMonth(12, 2025);

    expect(Swal.fire).toHaveBeenCalledOnceWith('Atención', 'No se pueden consultar o planificar datos anteriores al 2026.', 'warning');
    expect(component.selectedYear).toBe(2026);
  });
});
