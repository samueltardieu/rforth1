#include <linux/fs.h>
#include <linux/interrupt.h>
#include <linux/miscdevice.h>
#include <linux/module.h>
#include <linux/types.h>
#include <linux/wait.h>
#include <asm/uaccess.h>
#include <linux/spican.h>

/* Those constants reflect the mapping on the SHIX */
#define SPICAN_MINOR        245
#define SPICAN_REQUEST_IRQ  3
#define SPICAN_STATUS_IRQ   4
#define SPICAN_WRITE_BUFFER 0xA4000080
#define SPICAN_READ_BUFFER  0xA4000090
#define SPICAN_TOREAD       0xA40000A0
#define SPICAN_TOWRITE      0xA40000A4
#define SPICAN_STATUS       0xA40000A8
#define SPICAN_FLAGS        0xA40000AC

#define FLAG_ENABLE_RESET   (1<<0)  /* 0 to clear */
#define FLAG_ENABLE_INTR    (1<<1)  /* 1 to enable */

#define SETMASK    0x00
#define SETFILTER  0x01
#define READMSG    0x02
#define WRITEMSG   0x03
#define SETMODE    0x04

#define STATUS_VALID              0x80

#define STATUS_BIT(x) ((*spican_status&((x)|STATUS_VALID))==((x)|STATUS_VALID))
#define OK_TO_READ    STATUS_BIT(MESSAGE_READY)
#define OK_TO_WRITE   STATUS_BIT(TRANSMIT_BUFFER_AVAILABLE)

/* This wait queue gets signalled when an ongoing request completes */
static DECLARE_WAIT_QUEUE_HEAD(spican_request_wait_queue);

/* This wait queue gets signalled when a new status byte has been reloaded
 * by the Stratix */
static DECLARE_WAIT_QUEUE_HEAD(spican_status_wait_queue);

static volatile unsigned char *spican_write_buffer;
static volatile unsigned char *spican_read_buffer;
static volatile unsigned char *spican_towrite;
static volatile unsigned char *spican_toread;
static volatile unsigned char *spican_status;
static volatile unsigned char *spican_flags;

static unsigned char spican_pending;

static inline void
ack_request_irq (void)
{
  *spican_towrite = 0;
}

static irqreturn_t
spican_request_handler_irq (int irq, void *dev_id, struct pt_regs *regs)
{
  ack_request_irq ();
  spican_pending = 0;
  printk (KERN_INFO "handling request IRQ\n");
  wake_up_interruptible (&spican_request_wait_queue);
  return IRQ_HANDLED;
}

static irqreturn_t
spican_status_handler_irq (int irq, void *dev_id, struct pt_regs *regs)
{
  *spican_status;                     /* Ack the interrupt on the SHIX */
  printk (KERN_INFO "handling satus IRQ\n");
  wake_up_interruptible (&spican_status_wait_queue);
  return IRQ_HANDLED;
}

static void inline
spican_start_request (unsigned char towrite, unsigned char toread)
{
  *spican_toread = toread;
  spican_pending++;
  wmb();
  *spican_towrite = towrite;
}

static int
spican_read (struct file *file, char __user *buffer,
	      size_t count, loff_t *pos)
{
  wait_event_interruptible (spican_status_wait_queue,
			    spican_pending == 0 && OK_TO_READ);
  if (count > 12) count = 12;
  spican_write_buffer[0] = READMSG;
  spican_start_request (1, 12);
  wait_event_interruptible (spican_request_wait_queue, spican_pending == 0);
  return copy_to_user (buffer, spican_read_buffer, count) ? -EFAULT : count;
}

static int
spican_write (struct file *file, const char __user *buffer,
	       size_t count, loff_t *pos)
{
  wait_event_interruptible (spican_status_wait_queue,
			    spican_pending == 0 && OK_TO_WRITE);
  spican_write_buffer[0] = WRITEMSG;
  if (count > 12) count = 12;
  if (copy_from_user ((void *)(&spican_write_buffer[1]), buffer, count))
    return -EFAULT;
  spican_start_request (13, 0);
  return count;
}

static int
spican_ioctl_status (unsigned char *status)
{
  wait_event_interruptible
    (spican_status_wait_queue, *spican_status & STATUS_VALID);
  *status = *spican_status & 0x7f;
  return 0;
}

static int
spican_ioctl_setmask (struct spican_sid *arg)
{
  struct spican_sid larg;
  if (copy_from_user (&larg, arg, sizeof larg)) return -EFAULT;
  if (larg.n > 1 || larg.arbitration > 0x7ff) return -EINVAL;
  spican_write_buffer[0] = SETMASK;
  spican_write_buffer[1] = larg.arbitration & 0xff;
  spican_write_buffer[2] = larg.arbitration >> 8;
  spican_write_buffer[3] = larg.n;
  spican_start_request (4, 0);
  return 0;
}

static int
spican_ioctl_setfilter (struct spican_sid *arg)
{
  struct spican_sid larg;
  if (copy_from_user (&larg, arg, sizeof larg)) return -EFAULT;
  if (larg.n > 5 || larg.arbitration > 0x7ff) return -EINVAL;
  wait_event_interruptible (spican_request_wait_queue, spican_pending == 0);
  spican_write_buffer[0] = SETFILTER;
  spican_write_buffer[1] = larg.arbitration & 0xff;
  spican_write_buffer[2] = larg.arbitration >> 8;
  spican_write_buffer[3] = larg.n;
  spican_start_request (4, 0);
  return 0;
}

static int
spican_ioctl_setmode (unsigned char arg)
{
  if (arg != SPICAN_MODE_NORMAL && arg != SPICAN_MODE_LOOPBACK)
    return -EINVAL;
  wait_event_interruptible (spican_request_wait_queue, spican_pending == 0);
  spican_write_buffer[0] = SETMODE;
  spican_write_buffer[1] = arg;
  spican_start_request (2, 0);
  return 0;
}

static int
spican_ioctl_enableintr (unsigned char arg)
{
  if (arg) *spican_flags |= FLAG_ENABLE_INTR;
  else *spican_flags &= ~FLAG_ENABLE_INTR;
  return 0;
}

static int
spican_ioctl_enablereset (unsigned char arg)
{
  if (arg) *spican_flags |= FLAG_ENABLE_RESET;
  else *spican_flags &= ~FLAG_ENABLE_RESET;
  return 0;
}

static int
spican_ioctl (struct inode *inode, struct file *file,
	       unsigned int cmd, unsigned long arg)
{
  switch (_IOC_NR(cmd)) {
  case _IOC_NR(SPICAN_IOCTL_CHOOSE_SETMASK):
    return spican_ioctl_setmask ((struct spican_sid *) arg);
  case _IOC_NR(SPICAN_IOCTL_CHOOSE_SETFILTER):
    return spican_ioctl_setfilter ((struct spican_sid *) arg);
  case _IOC_NR(SPICAN_IOCTL_STATUS):
    return spican_ioctl_status ((unsigned char *) arg);
  case _IOC_NR(SPICAN_IOCTL_SET_MODE):
    return spican_ioctl_setmode (arg);
  case _IOC_NR(SPICAN_IOCTL_ENABLE_INTERRUPTS):
    return spican_ioctl_enableintr (arg);
  case _IOC_NR(SPICAN_IOCTL_ENABLE_RESET):
    return spican_ioctl_enablereset (arg);
  default:
    return -ENOTTY;
  }
}

static struct file_operations spican_fops = {
  .owner  = THIS_MODULE,
  .read   = spican_read,
  .write  = spican_write,
  .ioctl  = spican_ioctl,
};

static struct miscdevice spican_device = {
  SPICAN_MINOR,
  "spican",
  &spican_fops
};

static int __init
spican_init (void)
{
  if (misc_register (&spican_device)) {
    printk (KERN_WARNING "spican: could not register device\n");
    return -EBUSY;
  }
  if (request_irq (SPICAN_REQUEST_IRQ, spican_request_handler_irq,
		   SA_INTERRUPT, "spican", NULL)) {
    printk (KERN_WARNING "spican: request irq %d is not free\n",
	    SPICAN_REQUEST_IRQ);
    misc_deregister (&spican_device);
    return -EIO;
  }
  if (request_irq (SPICAN_STATUS_IRQ, spican_status_handler_irq,
		   SA_INTERRUPT, "spican", NULL)) {
    printk (KERN_WARNING "spican: status irq %d is not free\n",
	    SPICAN_STATUS_IRQ);
    free_irq (SPICAN_REQUEST_IRQ, NULL);
    misc_deregister (&spican_device);
    return -EIO;
  }
  spican_read_buffer  = (void *)SPICAN_READ_BUFFER;
  spican_write_buffer = (void *)SPICAN_WRITE_BUFFER;
  spican_towrite      = (void *)SPICAN_TOWRITE;
  spican_toread       = (void *)SPICAN_TOREAD;
  spican_status       = (void *)SPICAN_STATUS;
  spican_flags        = (void *)SPICAN_FLAGS;
  spican_pending      = 0;

  printk (KERN_INFO "Flags: 0x%x\n", *spican_flags);

  /* Force a reset at module loading time */
  spican_ioctl_enablereset (1);

  /* Allow interrupts to be generated */
  spican_ioctl_enableintr (1);

  /* Start module */
  spican_ioctl_enablereset (0);

  printk (KERN_INFO "Flags: 0x%x\n", *spican_flags);

  return 0;
}

static void __exit
spican_exit (void)
{
  spican_ioctl_enableintr (0);
  spican_ioctl_enablereset (1);
  free_irq (SPICAN_REQUEST_IRQ, NULL);
  free_irq (SPICAN_STATUS_IRQ, NULL);
  misc_deregister (&spican_device);
}

module_init(spican_init);
module_exit(spican_exit);

MODULE_AUTHOR("Samuel Tardieu <sam@rfc1149.net>");
MODULE_DESCRIPTION("SPICAN SPI/CAN interface driver");
MODULE_LICENSE("GPL");
