using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HospitalAppointmentSystem.Data;
using HospitalAppointmentSystem.Models;
using HospitalAppointmentSystem.ViewModels;

namespace HospitalAppointmentSystem.Services
{
    public interface IAppointmentService
    {
        Task<List<TimeSlotViewModel>> GetAvailableSlotsAsync(int doctorId, DateTime date);
        Task<(bool Success, string Message, Appointment? Appointment)> BookAppointmentAsync(int patientId, int doctorId, DateTime date, TimeSpan startTime, TimeSpan endTime, string reason);
        Task<(bool Success, string Message)> RescheduleAppointmentAsync(int appointmentId, int patientId, DateTime newDate, TimeSpan newStartTime, TimeSpan newEndTime);
        Task<(bool Success, string Message)> CancelAppointmentAsync(int appointmentId, int currentUserId, string userRole);
        Task<(bool Success, string Message)> UpdateStatusAsync(int appointmentId, string status, int doctorOrAdminUserId);
        Task<(bool Success, string Message)> AddConsultationNoteAsync(ConsultationNoteViewModel model);
        Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId);
    }

    public class AppointmentService : IAppointmentService
    {
        private readonly ApplicationDbContext _context;

        public AppointmentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<TimeSlotViewModel>> GetAvailableSlotsAsync(int doctorId, DateTime date)
        {
            var slots = new List<TimeSlotViewModel>();
            var targetDate = date.Date;

            // Past dates have no available slots
            if (targetDate < DateTime.Today)
            {
                return slots;
            }

            // Find Doctor Availability record for date
            var availability = await _context.DoctorAvailabilities
                .FirstOrDefaultAsync(a => a.DoctorId == doctorId && a.AvailableDate.Date == targetDate && a.IsAvailable);

            // Default business hours 09:00 to 17:00 if no explicit custom record found
            TimeSpan start = availability?.StartTime ?? new TimeSpan(9, 0, 0);
            TimeSpan end = availability?.EndTime ?? new TimeSpan(17, 0, 0);
            int duration = availability?.SlotDuration > 0 ? availability.SlotDuration : 30;

            // Fetch active appointments for doctor on targetDate
            var bookedAppointments = await _context.Appointments
                .Where(a => a.DoctorId == doctorId 
                            && a.AppointmentDate.Date == targetDate 
                            && a.Status != "Cancelled" 
                            && a.Status != "Rejected")
                .ToListAsync();

            TimeSpan current = start;
            while (current.Add(TimeSpan.FromMinutes(duration)) <= end)
            {
                TimeSpan slotEnd = current.Add(TimeSpan.FromMinutes(duration));
                bool isBooked = bookedAppointments.Any(a => 
                    (a.StartTime < slotEnd && a.EndTime > current));

                // If date is today, check if time has already passed
                if (targetDate == DateTime.Today && current <= DateTime.Now.TimeOfDay)
                {
                    isBooked = true;
                }

                DateTime displayTime = DateTime.Today.Add(current);
                slots.Add(new TimeSlotViewModel
                {
                    StartTime = current,
                    EndTime = slotEnd,
                    FormattedTime = displayTime.ToString("hh:mm tt"),
                    IsBooked = isBooked
                });

                current = slotEnd;
            }

            return slots;
        }

        public async Task<(bool Success, string Message, Appointment? Appointment)> BookAppointmentAsync(
            int patientId, int doctorId, DateTime date, TimeSpan startTime, TimeSpan endTime, string reason)
        {
            var targetDate = date.Date;

            if (targetDate < DateTime.Today)
            {
                return (false, "Cannot book an appointment for a past date.", null);
            }

            if (targetDate == DateTime.Today && startTime <= DateTime.Now.TimeOfDay)
            {
                return (false, "Cannot book a time slot in the past.", null);
            }

            // Double Booking Validation: Check for overlapping appointments with same doctor
            bool overlap = await _context.Appointments
                .AnyAsync(a => a.DoctorId == doctorId 
                            && a.AppointmentDate.Date == targetDate 
                            && a.Status != "Cancelled" 
                            && a.Status != "Rejected" 
                            && a.StartTime < endTime 
                            && a.EndTime > startTime);

            if (overlap)
            {
                return (false, "The selected time slot is no longer available. Please choose another slot.", null);
            }

            var appointment = new Appointment
            {
                PatientId = patientId,
                DoctorId = doctorId,
                AppointmentDate = targetDate,
                StartTime = startTime,
                EndTime = endTime,
                Reason = reason,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            await _context.Appointments.AddAsync(appointment);
            await _context.SaveChangesAsync();

            // Create notification for Doctor
            var doctor = await _context.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.DoctorId == doctorId);
            var patient = await _context.Patients.Include(p => p.User).FirstOrDefaultAsync(p => p.PatientId == patientId);

            if (doctor?.User != null)
            {
                await _context.Notifications.AddAsync(new Notification
                {
                    UserId = doctor.User.UserId,
                    Message = $"New appointment request from {patient?.User?.FullName ?? "a patient"} for {targetDate:MMM dd, yyyy} at {DateTime.Today.Add(startTime):hh:mm tt}.",
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            return (true, "Appointment requested successfully!", appointment);
        }

        public async Task<(bool Success, string Message)> RescheduleAppointmentAsync(
            int appointmentId, int patientId, DateTime newDate, TimeSpan newStartTime, TimeSpan newEndTime)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId && a.PatientId == patientId);

            if (appointment == null)
            {
                return (false, "Appointment not found or unauthorized access.");
            }

            if (appointment.Status == "Completed" || appointment.Status == "Cancelled")
            {
                return (false, $"Cannot reschedule a {appointment.Status.ToLower()} appointment.");
            }

            var targetDate = newDate.Date;
            if (targetDate < DateTime.Today)
            {
                return (false, "Cannot reschedule to a past date.");
            }

            bool overlap = await _context.Appointments
                .AnyAsync(a => a.AppointmentId != appointmentId 
                            && a.DoctorId == appointment.DoctorId 
                            && a.AppointmentDate.Date == targetDate 
                            && a.Status != "Cancelled" 
                            && a.Status != "Rejected" 
                            && a.StartTime < newEndTime 
                            && a.EndTime > newStartTime);

            if (overlap)
            {
                return (false, "The chosen time slot is already booked. Please select another slot.");
            }

            appointment.AppointmentDate = targetDate;
            appointment.StartTime = newStartTime;
            appointment.EndTime = newEndTime;
            appointment.Status = "Pending"; // Re-evaluate confirmation on reschedule
            appointment.UpdatedAt = DateTime.UtcNow;

            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();

            if (appointment.Doctor?.User != null)
            {
                await _context.Notifications.AddAsync(new Notification
                {
                    UserId = appointment.Doctor.User.UserId,
                    Message = $"Appointment #{appointment.AppointmentId} rescheduled to {targetDate:MMM dd, yyyy} at {DateTime.Today.Add(newStartTime):hh:mm tt}.",
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            return (true, "Appointment rescheduled successfully!");
        }

        public async Task<(bool Success, string Message)> CancelAppointmentAsync(int appointmentId, int currentUserId, string userRole)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appointment == null)
            {
                return (false, "Appointment not found.");
            }

            if (userRole == "Patient" && appointment.Patient.UserId != currentUserId)
            {
                return (false, "Unauthorized action.");
            }

            if (userRole == "Doctor" && appointment.Doctor.UserId != currentUserId)
            {
                return (false, "Unauthorized action.");
            }

            if (appointment.Status == "Completed")
            {
                return (false, "Completed appointments cannot be cancelled.");
            }

            appointment.Status = "Cancelled";
            appointment.UpdatedAt = DateTime.UtcNow;

            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();

            return (true, "Appointment cancelled successfully.");
        }

        public async Task<(bool Success, string Message)> UpdateStatusAsync(int appointmentId, string status, int doctorOrAdminUserId)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appointment == null)
            {
                return (false, "Appointment not found.");
            }

            appointment.Status = status;
            appointment.UpdatedAt = DateTime.UtcNow;

            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();

            if (appointment.Patient?.User != null)
            {
                await _context.Notifications.AddAsync(new Notification
                {
                    UserId = appointment.Patient.User.UserId,
                    Message = $"Your appointment with Dr. {appointment.Doctor.User.FullName} on {appointment.AppointmentDate:MMM dd} is now '{status}'.",
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            return (true, $"Appointment status updated to '{status}'.");
        }

        public async Task<(bool Success, string Message)> AddConsultationNoteAsync(ConsultationNoteViewModel model)
        {
            var appointment = await _context.Appointments.FindAsync(model.AppointmentId);
            if (appointment == null)
            {
                return (false, "Appointment not found.");
            }

            var existingNote = await _context.ConsultationNotes
                .FirstOrDefaultAsync(c => c.AppointmentId == model.AppointmentId);

            if (existingNote != null)
            {
                existingNote.Notes = model.Notes;
                existingNote.Prescription = model.Prescription ?? string.Empty;
                _context.ConsultationNotes.Update(existingNote);
            }
            else
            {
                var note = new ConsultationNote
                {
                    AppointmentId = model.AppointmentId,
                    DoctorId = model.DoctorId,
                    Notes = model.Notes,
                    Prescription = model.Prescription ?? string.Empty,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.ConsultationNotes.AddAsync(note);
            }

            appointment.Status = "Completed";
            appointment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return (true, "Consultation notes and prescription recorded successfully.");
        }

        public async Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId)
        {
            return await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Doctor).ThenInclude(d => d.Department)
                .Include(a => a.ConsultationNote)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);
        }
    }
}
