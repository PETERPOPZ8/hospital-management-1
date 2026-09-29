document.addEventListener('DOMContentLoaded', function () {
    
    // 1. Password Visibility Toggle
    const togglePasswordButtons = document.querySelectorAll('.toggle-password');
    togglePasswordButtons.forEach(button => {
        button.addEventListener('click', function () {
            const input = this.previousElementSibling || document.querySelector(this.dataset.target);
            if (input && input.type === 'password') {
                input.type = 'text';
                this.innerHTML = '<i class="bi bi-eye-slash"></i>';
            } else if (input) {
                input.type = 'password';
                this.innerHTML = '<i class="bi bi-eye"></i>';
            }
        });
    });

    // 2. Dynamic Time Slot Fetcher for Booking & Rescheduling
    const doctorSelect = document.getElementById('doctorIdSelect');
    const dateInput = document.getElementById('appointmentDateInput');
    const slotContainer = document.getElementById('slotContainer');
    const selectedSlotHiddenInput = document.getElementById('selectedSlotHiddenInput');

    function fetchAvailableSlots() {
        if (!doctorSelect || !dateInput || !slotContainer) return;

        const doctorId = doctorSelect.value;
        const date = dateInput.value;

        if (!doctorId || !date) {
            slotContainer.innerHTML = '<div class="alert alert-info small">Please select a doctor and date to view time slots.</div>';
            return;
        }

        slotContainer.innerHTML = '<div class="text-center py-3"><div class="spinner-border spinner-border-sm text-primary" role="status"></div> Loading available time slots...</div>';

        fetch(`/api/appointment/slots?doctorId=${doctorId}&date=${date}`)
            .then(response => {
                if (!response.ok) throw new Error('Failed to fetch slots.');
                return response.json();
            })
            .then(slots => {
                if (!slots || slots.length === 0) {
                    slotContainer.innerHTML = '<div class="alert alert-warning small mb-0"><i class="bi bi-exclamation-triangle-fill me-1"></i> No available time slots found for this date. Please select another date or doctor.</div>';
                    return;
                }

                let html = '<div class="slot-grid">';
                slots.forEach(slot => {
                    const isBooked = slot.isBooked;
                    const startTimeStr = slot.startTime.substring(0, 5); // "09:00"
                    const disabledClass = isBooked ? 'disabled' : '';
                    
                    html += `
                        <div class="slot-btn ${disabledClass}" data-time="${startTimeStr}">
                            <i class="bi bi-clock me-1"></i> ${slot.formattedTime}
                        </div>
                    `;
                });
                html += '</div>';
                slotContainer.innerHTML = html;

                // Re-bind click events on newly rendered slot buttons
                document.querySelectorAll('.slot-btn:not(.disabled)').forEach(btn => {
                    btn.addEventListener('click', function () {
                        document.querySelectorAll('.slot-btn').forEach(b => b.classList.remove('selected'));
                        this.classList.add('selected');
                        if (selectedSlotHiddenInput) {
                            selectedSlotHiddenInput.value = this.dataset.time;
                        }
                    });
                });
            })
            .catch(err => {
                console.error(err);
                slotContainer.innerHTML = '<div class="alert alert-danger small">Error fetching slots. Please try again.</div>';
            });
    }

    if (doctorSelect && dateInput) {
        doctorSelect.addEventListener('change', fetchAvailableSlots);
        dateInput.addEventListener('change', fetchAvailableSlots);
        // Initial fetch if values present
        if (doctorSelect.value && dateInput.value) {
            fetchAvailableSlots();
        }
    }

    // 3. Confirmation Dialogs for Forms with data-confirm attribute
    const confirmForms = document.querySelectorAll('form[data-confirm]');
    confirmForms.forEach(form => {
        form.addEventListener('submit', function (e) {
            const message = this.dataset.confirm || 'Are you sure you want to perform this action?';
            if (!confirm(message)) {
                e.preventDefault();
            }
        });
    });
});
