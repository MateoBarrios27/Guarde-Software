import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CommunicationsComponent } from './communications.component';

describe('CommunicationsComponent', () => {
  let component: CommunicationsComponent;
  let fixture: ComponentFixture<CommunicationsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CommunicationsComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CommunicationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
